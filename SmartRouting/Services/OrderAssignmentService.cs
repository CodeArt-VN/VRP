using System.Linq;
using Azure.Core;
using SmartRouting.Configurations;
using SmartRouting.Models;
using NetTopologySuite.Geometries; // Required for Point
using Google.OrTools.ConstraintSolver; // Required for OR-Tools

namespace SmartRouting.Services
{
	public class OrderAssignmentService
	{
		public CalcOption? _option { get; set; }
		private readonly ApplicationDbContext _context;
		private readonly GeometryFactory _geometryFactory; 
		private readonly DistanceService _distanceService;

		public OrderAssignmentService(ApplicationDbContext context, DistanceService distanceService, CalcOption? option = null)
		{
			_context = context ?? throw new ArgumentNullException(nameof(context));
			_distanceService = distanceService ?? throw new ArgumentNullException(nameof(distanceService));
			_option = option;
			_geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326); // Initialize with SRID 4326
		}

		public RouteCalcResponse CalculateRoutes(List<Vehicle>? vehicles, List<DeliveryOrder>? orders, int iDDepotAddress)
		{
			// Pseudocode plan for processing route calculation based on requirements in the guide file:
			// 1. Validate input data (already done above).
			// 2. Get the list of delivery addresses from Address based on IDAddress.
			//    Check which orders do not have a delivery address.
			// 3. Assign orders based on Options:
			//    First group orders by distance, then by weight and volume.
			//    Priority setting: Weight, Volume, Deadline, Distance....
			//    When assigning orders to vehicles, check the vehicle capacity (weight, volume) constraints by weightOption and volumeOption.
			// 4. Return the list of Shipments and UnassignedOrders in RouteCalcResponse.

			// 1. Validate input data
			if (vehicles == null || orders == null)
			{
				throw new ArgumentException("Invalid request: Vehicles or Orders are null.");
			}

			var shipments = new List<Shipment>();
			var unassignedOrders = new List<UnassignedOrder>();

			// 2. Get the list of delivery addresses from Address based on IDAddress.
			//    Check which orders do not have a delivery address.
			var deliveryAddresses = GetDeliveryAddresses(orders.Select(o => o.IDAddress).Distinct().ToList());
			var ordersWithoutAddress = orders.Where(o => !deliveryAddresses.Any(a => a.Id == o.IDAddress)).ToList();
			if (ordersWithoutAddress.Any())
			{
				unassignedOrders.AddRange(ordersWithoutAddress.Select(o => new UnassignedOrder
				{
					IDOrder = o.Id,
					Reason = "No delivery address found."
				}));

				// Remove orders without address from the original list
				orders = orders.Except(ordersWithoutAddress).ToList();
			}

			// 3. Assign orders based on Options
			shipments = AssignOrdersToVehicles(vehicles, orders, deliveryAddresses, iDDepotAddress);


			// 4. Return the list of Shipments and UnassignedOrders in RouteCalcResponse.
			return new RouteCalcResponse
			{
				Shipments = shipments,
				UnassignedOrders = unassignedOrders
			};
		}

		private List<Shipment> AssignOrdersToVehicles(List<Vehicle> vehicles, List<DeliveryOrder> orders, List<Address> deliveryAddresses, int iDDepotAddress)
		{
			List<IndexDistance> cachedDistances = _distanceService.GetIndicesDistance(deliveryAddresses.Select(a => a.Id).ToList());


			// Initialize OR-Tools routing model
			// RoutingIndexManager maps nodes (orders + depot) to indices used by the solver
			var manager = new RoutingIndexManager(orders.Count + 1, vehicles.Count, 0); // +1 for depot, vehicles.Count for number of vehicles
			var routing = new RoutingModel(manager);

			// Create distance callback
			// This callback calculates the distance between two nodes (fromIndex and toIndex)
			var distanceCallback = new LongLongToLong((fromIndex, toIndex) =>
			{
				var fromNode = manager.IndexToNode((int)fromIndex); // Map index to node
				var toNode = manager.IndexToNode((int)toIndex); // Map index to node

				if (fromNode == 0 || toNode == 0) // Depot node
				{
					return 0; // No distance cost for depot
				}

				// Get orders corresponding to the nodes
				var fromOrder = orders[fromNode - 1]; // -1 because depot is at index 0
				var toOrder = orders[toNode - 1];

				// Get locations of the orders
				var fromAddress = deliveryAddresses.FirstOrDefault(a => a.Id == fromOrder.IDAddress);
				var toAddress = deliveryAddresses.FirstOrDefault(a => a.Id == toOrder.IDAddress);

				if (fromAddress == null || toAddress == null)
				{
					return long.MaxValue; // Return a large value if location is missing
				}

				// Calculate distance and convert to meters
				return (long)_distanceService.CalculateDistance(fromAddress, toAddress, cachedDistances);
			});

			// Register the distance callback with the routing model
			var transitCallbackIndex = routing.RegisterTransitCallback(distanceCallback);
			routing.SetArcCostEvaluatorOfAllVehicles(transitCallbackIndex); // Set cost evaluator for all vehicles

			// Add capacity constraints for each vehicle
			foreach (var vehicle in vehicles)
			{
				routing.AddDimension(
					transitCallbackIndex, // Distance callback index
					0, // No slack (no extra capacity allowed)
					(int)(vehicle.MaxWeight * 1000), // Vehicle capacity in grams
					true, // Start cumul to zero
					"Capacity" // Dimension name
				);
			}

			// Define search parameters for the solver
			var searchParameters = operations_research_constraint_solver.DefaultRoutingSearchParameters();
			searchParameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.PathCheapestArc; // Use cheapest arc heuristic

			// Solve the problem
			var solution = routing.SolveWithParameters(searchParameters);

			// Process the solution
			var shipments = new List<Shipment>();

			if (solution != null)
			{
				for (int i = 0; i < vehicles.Count; i++)
				{
					var route = new List<RoutePoint>(); // List to store the route for the current vehicle
					var index = routing.Start(i); // Get the starting index for the vehicle

					while (!routing.IsEnd(index)) // Loop until the end of the route
					{
						var node = manager.IndexToNode(index); // Map index to node
						if (node > 0) // Skip depot
						{
							var order = orders[node - 1]; // Get the order corresponding to the node
							var address = _context.Addresses.FirstOrDefault(a => a.Id == order.IDAddress); // Get the address of the order

							if (address?.Location != null)
							{
								// Add the route point to the list
								route.Add(new RoutePoint
								{
									IDAddress = address.Id,
									IDOrder = order.Id,
									Sequence = route.Count + 1, // Sequence number in the route
									Latitude = address.Location.Y, // Latitude of the address
									Longitude = address.Location.X // Longitude of the address
								});
							}
						}

						index = solution.Value(routing.NextVar(index)); // Move to the next index in the route
					}

					// Add the shipment for the current vehicle
					shipments.Add(new Shipment
					{
						IDVehicle = vehicles[i].Id, // Vehicle ID
						Route = route, // Route for the vehicle
						TotalDistance = solution.ObjectiveValue() / 1000.0, // Total distance in kilometers
						TotalTime = 0 // Placeholder for time calculation (to be implemented later)
					});
				}
			}

			return shipments; // Return the list of shipments
		}

		private List<Address> GetDeliveryAddresses(List<int> list)
		{
			//Lấy danh sách địa chỉ giao hàng từ Address theo IDAddress
			return _context.Addresses.Where(a => list.Contains(a.Id)).Select(s => new Address
			{
				Id = s.Id,
				Location = s.Location
			}).ToList();
		}



	}
}
