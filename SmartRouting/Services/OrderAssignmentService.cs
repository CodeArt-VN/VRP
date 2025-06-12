using System.Linq;
using Azure.Core;
using SmartRouting.Configurations;
using SmartRouting.Models;
using NetTopologySuite.Geometries; // Required for Point
using Google.OrTools.ConstraintSolver;
using Google.Protobuf.WellKnownTypes; // Required for OR-Tools
using SmartRouting.Services;
using System.Reflection.Emit; // For RoutingStrategies

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


			var unassignedOrders = new List<UnassignedOrder>();

			// 2. Get the list of delivery addresses from Address based on IDAddress.
			//    Check which orders do not have a delivery address.
			var soIds = orders.Select(o => o.IDAddress).Distinct().ToList();
			soIds.Add(iDDepotAddress); // Add depot address to the list
			var deliveryAddresses = GetDeliveryAddresses(soIds);
			var ordersWithoutAddress = orders.Where(o => !deliveryAddresses.Any(a => a.Id == o.IDAddress)).ToList();

			// Ensure depot address is included in the delivery addresses
			if (!deliveryAddresses.Any(a => a.Id == iDDepotAddress))
			{
				throw new Exception($"Depot address with ID {iDDepotAddress} not found.");
			}

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

			// Fill address, weight, volume for orders
			foreach (var order in orders)
			{
				var address = deliveryAddresses.FirstOrDefault(a => a.Id == order.IDAddress);
				if (address != null)
				{
					order.Address = address;
				}

				// Assuming Weight and Volume are properties of DeliveryOrder
				order.Weight = order.OrderLines?.Sum(ol => ol.Weight * ol.Quantity) ?? 0;
				order.Volume = order.OrderLines?.Sum(ol => ol.Volume * ol.Quantity) ?? 0;
			}

			// Check Orders that exceed weight constraints config
			if (_option?.Constraints.Weight != FillOption.None)
			{
				var weightLimit = _option.Constraints.Weight switch
				{
					FillOption.Min => vehicles.Max(v => v.WeightMin),
					FillOption.Recommended => vehicles.Max(v => v.WeightRecommended),
					FillOption.Max => vehicles.Max(v => v.WeightMax)
				};

				var ordersExceedingWeight = orders.Where(o => o.Weight > weightLimit).ToList();
				if (ordersExceedingWeight.Any())
				{
					unassignedOrders.AddRange(ordersExceedingWeight.Select(o => new UnassignedOrder
					{
						IDOrder = o.Id,
						Reason = "Order exceeds weight constraints."
					}));

					// Remove orders exceeding weight from the original list
					orders = orders.Except(ordersExceedingWeight).ToList();
				}
			}

			// Check Orders that exceed volume constraints config
			if (_option?.Constraints.Volume != FillOption.None)
			{
				var volumeLimit = _option.Constraints.Volume switch
				{
					FillOption.Min => vehicles.Max(v => v.VolumeMin),
					FillOption.Recommended => vehicles.Max(v => v.VolumeRecommended),
					FillOption.Max => vehicles.Max(v => v.VolumeMax)
				};

				var ordersExceedingVolume = orders.Where(o => o.Volume > volumeLimit).ToList();
				if (ordersExceedingVolume.Any())
				{
					unassignedOrders.AddRange(ordersExceedingVolume.Select(o => new UnassignedOrder
					{
						IDOrder = o.Id,
						Reason = "Order exceeds volume constraints."
					}));

					// Remove orders exceeding volume from the original list
					orders = orders.Except(ordersExceedingVolume).ToList();
				}
			}




			// Merge the depot to node list at the beginning
			var depotAddress = deliveryAddresses.FirstOrDefault(a => a.Id == iDDepotAddress);
			if (depotAddress != null)
			{
				orders.Insert(0, new DeliveryOrder
				{
					IDAddress = depotAddress.Id,
					Address = depotAddress
				});
			}
			else
			{
				throw new Exception($"Depot address with ID {iDDepotAddress} not found.");
			}

			List<IndexDistance> cachedDistances = _distanceService.GetIndicesDistance(deliveryAddresses.Select(a => a.Id).ToList());

			int trip = 1;
			var shipments = new List<Shipment>();

		AssignRemainingOrders:
			// 3. Assign orders based on Options
			shipments.AddRange(AssignOrdersToVehicles(vehicles, orders, cachedDistances, trip));

			// Remove those that have been assigned to shipments
			orders.RemoveAll(o => shipments.SelectMany(s => s.Route).Select(r => r.IDOrder).Contains(o.Id));

			// Run again if there are still unassigned orders
			if (orders.Any(d => d.IDAddress != depotAddress.Id))
			{
				// Remove vehicle with ID -1 (the huge fallback vehicle)
				vehicles.RemoveAll(v => v.Id == -1);
				trip++;

				// Sort vehicles by total time Ascending to fill the vehicle with the least time first
				// Calculate total time for each vehicle based on current shipments
				var vehicleTimeDict = shipments
					.GroupBy(s => s.IDVehicle)
					.ToDictionary(g => g.Key, g => g.Sum(s => s.TotalTime));

				vehicles = vehicles
					.OrderBy(v => vehicleTimeDict.ContainsKey(v.Id) ? vehicleTimeDict[v.Id] : 0)
					.ToList();

				goto AssignRemainingOrders;
			}


			// 4. Return the list of Shipments and UnassignedOrders in RouteCalcResponse.
			return new RouteCalcResponse
			{
				Shipments = shipments,
				UnassignedOrders = unassignedOrders
			};
		}

		private List<Shipment> AssignOrdersToVehicles(List<Vehicle> vehicles, List<DeliveryOrder> orders, List<IndexDistance> cachedDistances, int trip)
		{
			Console.WriteLine($"Bắt đầu phân tài cho {orders.Count} đơn hàng và {vehicles.Count} xe");
			var startTime = DateTime.Now;

			// Add 1 huge vehicle to the list to handle all orders when all vehicles are full

			vehicles.Add(new Vehicle
			{
				Id = -1,
				WeightMax = 1000000000,
				VolumeMax = 1000000000,
				WeightMin = 0,
				VolumeMin = 0,
				WeightRecommended = 1000000000,
				VolumeRecommended = 1000000000
			});

			var manager = new RoutingIndexManager(orders.Count, vehicles.Count, 0);
			var routing = new RoutingModel(manager);

			// Create a dictionary to store calculated distances
			// Using normalized keys (smaller ID first) to avoid duplicating distance calculations
			// since distance from A to B equals distance from B to A
			var calculatedDistances = new Dictionary<(int, int), double>();


			/// Define cost for each vehicle
			for (int i = 0; i < vehicles.Count; i++)
			{
				int vehicleIndex = i;
				routing.SetArcCostEvaluatorOfVehicle(routing.RegisterTransitCallback((fromIndex, toIndex) =>
				{
					if (vehicleIndex == vehicles.Count - 1)
						return 9000000; // Huge vehicle has extremely high travel cost

					int fromNode = manager.IndexToNode(fromIndex);
					int toNode = manager.IndexToNode(toIndex);

					if (fromNode == 0 || toNode == 0) return 0; // Cost from/to depot is 0

					var fromAddress = orders[fromNode].Address; // Node > 0 corresponds to orders[node]
					var toAddress = orders[toNode].Address; // Node > 0 corresponds to orders[node]

					if (fromAddress == null || toAddress == null) return long.MaxValue;

					// Create a normalized key to ensure we only store one distance per pair
					// regardless of direction (A→B or B→A)
					var key = fromAddress.Id < toAddress.Id
						? (fromAddress.Id, toAddress.Id)
						: (toAddress.Id, fromAddress.Id);

					if (!calculatedDistances.ContainsKey(key)) calculatedDistances[key] = _distanceService.CalculateDistance(fromAddress, toAddress, ref cachedDistances);

					return (long)calculatedDistances[key];
				}), vehicleIndex);

				// Add fixed cost for vehicle i
				long fixedVehicleCost;
				if (i != vehicles.Count - 1)
					fixedVehicleCost = i * 100000; // Priority vehicles have fixed cost based on their index
				else
					fixedVehicleCost = 99000000000; // Very high fixed cost for the huge fallback vehicle

				// Set fixed cost for vehicle using OR-Tools API
				routing.SetFixedCostOfVehicle(fixedVehicleCost, i);

			}


			//Check if the vehicle has weight constraint
			if (_option?.Constraints.Weight != FillOption.None)
			{
				var weightLimits = vehicles.Select(v => (
						_option?.Constraints.Weight == FillOption.Min ? (long)v.WeightMin :
						_option?.Constraints.Weight == FillOption.Recommended ? (long)v.WeightRecommended :
						(long)v.WeightMax
					) * 1).ToArray();
				// Add weight constraint
				routing.AddDimensionWithVehicleCapacity(
					routing.RegisterUnaryTransitCallback((long index) =>
					{
						var node = manager.IndexToNode(index);
						if (node == 0) return 0; // Depot node has zero weight
						return (int)orders[node].Weight; // Node > 0 corresponds to orders[node]
					}), 0,
					weightLimits, // Tải trọng tối đa cho từng xe
					true, "Weight"
				);
			}

			// Check if the vehicle has volume constraint
			if (_option?.Constraints.Volume != FillOption.None)
			{
				// Add volume constraint
				routing.AddDimensionWithVehicleCapacity(
					routing.RegisterUnaryTransitCallback((long index) =>
					{
						var node = manager.IndexToNode(index);
						if (node == 0) return 0; // Depot node has zero volume
						return (int)orders[node].Volume; // Node > 0 corresponds to orders[node]
					}), 0,
					vehicles.Select(v => (
						_option?.Constraints.Volume == FillOption.Min ? (long)v.VolumeMin :
						_option?.Constraints.Volume == FillOption.Recommended ? (long)v.VolumeRecommended :
						(long)v.VolumeMax
					) * 1
					).ToArray(), // Thể tích tối đa cho từng xe
					true, "Volume"
				);
			}

			// Setting first solution heuristic.
			RoutingSearchParameters searchParameters = operations_research_constraint_solver.DefaultRoutingSearchParameters();
			searchParameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.PathCheapestArc;
			searchParameters.LocalSearchMetaheuristic = LocalSearchMetaheuristic.Types.Value.GuidedLocalSearch;

			var seconds = 5 + 0.5 * orders.Count + 0.2 * vehicles.Count;
			searchParameters.TimeLimit = new Duration { Seconds = (long)seconds };
			searchParameters.LnsTimeLimit = new Duration { Seconds = (long)(seconds / 4) };

			// searchParameters.TimeLimit = new Duration { Seconds = 30 }; // Overall time limit for solution
			// searchParameters.LnsTimeLimit = new Duration { Seconds = 1 }; // Time limit for completion search of each local search neighbor
			searchParameters.LogSearch = true; // Enable search logging for better troubleshooting
			searchParameters.SolutionLimit = orders.Count * 5; // Limit the number of solutions to find
			var solution = routing.SolveWithParameters(searchParameters);



			//Try multiple strategies if needed to find a solution
			//var solution = RoutingStrategies.TryMultipleSolutionStrategies(routing,orders.Count, vehicles.Count);

			var endTime = DateTime.Now;
			Console.WriteLine($"Thời gian chạy thuật toán: {(endTime - startTime).TotalMilliseconds} ms");

			var avgSpeed = 30; // Average speed in km/h
			var shipments = new List<Shipment>();


			if (solution != null)
			{
				for (int i = 0; i < vehicles.Count; i++)
				{
					//Check if the vehicle is the huge fallback vehicle
					if (vehicles[i].Id == -1)
						continue; // Skip the huge fallback vehicle

					var route = new List<RoutePoint>();
					var index = routing.Start(i);
					decimal totalWeight = 0;
					decimal totalVolume = 0;
					double totalDistance = 0;
					double preToThisDistance = 0; // Distance from previous to current node
					int totalTime = 0;

					while (!routing.IsEnd(index))
					{
						var node = manager.IndexToNode(index);
						if (node > 0)
						{
							var order = orders[node]; // Node > 0 corresponds to orders[node]
							var address = order.Address;

							if (address?.Location != null)
							{
								route.Add(new RoutePoint
								{
									IDAddress = address.Id,
									IDOrder = order.Id,
									Sequence = route.Count + 1,
									Latitude = address.Location.Y,
									Longitude = address.Location.X,
									StartTime = totalTime, // Assign StartTime directly
									Distance = preToThisDistance,
								});
							}

							totalWeight += order.Weight;
							totalVolume += order.Volume;
							totalTime += 15; // Add 15 minutes for each stop
						}

						var nextIndex = solution.Value(routing.NextVar(index));
						if (!routing.IsEnd(nextIndex))
						{
							var fromNode = manager.IndexToNode(index);
							var toNode = manager.IndexToNode(nextIndex);

							if (fromNode > 0 && toNode > 0)
							{
								var fromAddress = orders[fromNode].Address; // Node > 0 corresponds to orders[node]
								var toAddress = orders[toNode].Address; // Node > 0 corresponds to orders[node]

								if (fromAddress != null && toAddress != null)
								{
									// Use the same normalized key format as before
									var key = fromAddress.Id < toAddress.Id
										? (fromAddress.Id, toAddress.Id)
										: (toAddress.Id, fromAddress.Id);

									var distance = calculatedDistances.ContainsKey(key)
										? calculatedDistances[key]
										: _distanceService.CalculateDistance(fromAddress, toAddress, ref cachedDistances);

									// If the distance wasn't in our dictionary, store it now
									if (!calculatedDistances.ContainsKey(key))
									{
										calculatedDistances[key] = distance;
									}
									preToThisDistance = distance; // Update distance from previous to current node
									totalDistance += distance;
									totalTime += (int)(distance / avgSpeed * 60); // Convert hours to minutes
								}
							}
						}

						index = nextIndex;
					}

					decimal vehicleMaxWeight =
						(_option?.Constraints.Weight == FillOption.Min ? vehicles[i].WeightMin :
						_option?.Constraints.Weight == FillOption.Recommended ? vehicles[i].WeightRecommended :
						vehicles[i].WeightMax) * 1; // Convert to kg
					decimal vehicleMaxVolume =
						(_option?.Constraints.Volume == FillOption.Min ? vehicles[i].VolumeMin :
						_option?.Constraints.Volume == FillOption.Recommended ? vehicles[i].VolumeRecommended :
						vehicles[i].VolumeMax) * 1; // Convert to liters

					// Only add non-empty routes
					if (route.Count > 0)
					{
						shipments.Add(new Shipment
						{
							IDVehicle = vehicles[i].Id,
							Trip = trip,
							Route = route,
							TotalDistance = totalDistance,
							TotalTime = totalTime,
							TotalWeight = totalWeight,
							TotalVolume = totalVolume,
							WeightRate = vehicleMaxWeight > 0 ? totalWeight / vehicleMaxWeight : 0,
							VolumeRate = vehicleMaxVolume > 0 ? totalVolume / vehicleMaxVolume : 0
						});
					}
				}
			}
			else
			{
				Console.WriteLine("WARNING: All solution strategies failed to find a valid solution");
			}

			var endProcessingTime = DateTime.Now;
			Console.WriteLine($"Tổng thời gian xử lý: {(endProcessingTime - startTime).TotalMilliseconds} ms");

			return shipments;
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


		private RoutingSearchParameters SolutionStrategy()
		{
			Console.WriteLine("Warning: ---------------------- SolutionStrategy ----------------------");
			var searchParameters = operations_research_constraint_solver.DefaultRoutingSearchParameters();

			// Map the strategy from CalcOption to OR-Tools FirstSolutionStrategy
			switch (_option?.SolutionStrategy?.ToUpperInvariant())
			{
				case "CHEAPEST":
					searchParameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.PathCheapestArc;
					break;
				case "SAVINGS":
					searchParameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.Savings;
					break;
				case "SWEEP":
					searchParameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.Sweep;
					break;
				default:
					searchParameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.PathCheapestArc; // Default to CHEAPEST
					break;
			}

			return searchParameters;
		}


	}
}
