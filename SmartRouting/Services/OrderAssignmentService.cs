using System.Linq;
using Azure.Core;
using SmartRouting.Configurations;
using SmartRouting.Models;
using NetTopologySuite.Geometries; // Required for Point
using Google.OrTools.ConstraintSolver;
using Google.Protobuf.WellKnownTypes; // Required for OR-Tools

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
			var soIds = orders.Select(o => o.IDAddress).Distinct().ToList();
			soIds.Add(iDDepotAddress); // Add depot address to the list
			var deliveryAddresses = GetDeliveryAddresses(soIds);
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

			// 3. Assign orders based on Options
			shipments = AssignOrdersToVehicles(vehicles, orders, cachedDistances);

			// Check if there are any unassigned orders then add them to unassignedOrders
			var unassignedOrderIds = orders.Select(o => o.Id).Except(shipments.SelectMany(s => s.Route).Select(r => r.IDOrder)).ToList();
			if (unassignedOrderIds.Any())
			{
				unassignedOrders.AddRange(unassignedOrderIds.Select(id => new UnassignedOrder
				{
					IDOrder = id,
					Reason = "No vehicle available."
				}));
			}

			// 4. Return the list of Shipments and UnassignedOrders in RouteCalcResponse.
			return new RouteCalcResponse
			{
				Shipments = shipments,
				UnassignedOrders = unassignedOrders
			};
		}

		private List<Shipment> AssignOrdersToVehicles(List<Vehicle> vehicles, List<DeliveryOrder> orders, List<IndexDistance> cachedDistances)
		{
			Console.WriteLine("Warning: ---------------------- AssignOrdersToVehicles ----------------------");
			var manager = new RoutingIndexManager(orders.Count, vehicles.Count, 0);
			var routing = new RoutingModel(manager);

			// Create a dictionary to store calculated distances
			// Using normalized keys (smaller ID first) to avoid duplicating distance calculations
			// since distance from A to B equals distance from B to A
			var calculatedDistances = new Dictionary<(int, int), double>();

			//// Define cost of each arc.
			routing.SetArcCostEvaluatorOfAllVehicles(routing.RegisterTransitCallback((fromIndex, toIndex) =>
			{
				int fromNode = manager.IndexToNode(fromIndex);
				int toNode = manager.IndexToNode(toIndex);

				if (fromNode == 0 || toNode == 0) return 0;

				var fromAddress = orders[fromNode - 1].Address;
				var toAddress = orders[toNode - 1].Address;

				if (fromAddress == null || toAddress == null) return long.MaxValue;

				// Create a normalized key to ensure we only store one distance per pair
				// regardless of direction (A→B or B→A)
				var key = fromAddress.Id < toAddress.Id 
					? (fromAddress.Id, toAddress.Id) 
					: (toAddress.Id, fromAddress.Id);

				if (!calculatedDistances.ContainsKey(key))
				{
					// Calculate the distance and store it in our dictionary
					calculatedDistances[key] = _distanceService.CalculateDistance(fromAddress, toAddress, ref cachedDistances);
				}

				return (long)calculatedDistances[key];
			}));


			//Check if the vehicle has weight constraint
			if (_option?.Constraints.Weight != FillOption.None)
			{
				// Add weight constraint
				routing.AddDimensionWithVehicleCapacity(
					routing.RegisterUnaryTransitCallback((long index) =>
					{
						var node = manager.IndexToNode(index);
						return node == 0 ? 0 : (int)orders[node - 1].Weight;
					}), 0,
					vehicles.Select(v => (
						_option?.Constraints.Weight == FillOption.Min ? (long)v.MinWeight :
						_option?.Constraints.Weight == FillOption.Recommended ? (long)v.RecommendedWeight :
						(long)v.MaxWeight
					) * 1
					).ToArray(), // Tải trọng tối đa cho từng xe
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
						return node == 0 ? 0 : (int)orders[node - 1].Volume;
					}), 0,
					vehicles.Select(v => (
						_option?.Constraints.Volume == FillOption.Min ? (long)v.MinVolume :
						_option?.Constraints.Volume == FillOption.Recommended ? (long)v.RecommendedVolume :
						(long)v.MaxVolume
					) * 1 
					).ToArray(), // Thể tích tối đa cho từng xe
					true, "Volume"
				);
			}


			// Setting first solution heuristic.
			RoutingSearchParameters searchParameters = operations_research_constraint_solver.DefaultRoutingSearchParameters();
			searchParameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.PathCheapestArc;
			searchParameters.LocalSearchMetaheuristic = LocalSearchMetaheuristic.Types.Value.GuidedLocalSearch;
			searchParameters.TimeLimit = new Duration { Seconds = 30 }; // Overall time limit for solution
			searchParameters.LnsTimeLimit = new Duration { Seconds = 1 }; // Time limit for completion search of each local search neighbor
			//searchParameters.SolutionLimit = 100; // Limit the number of solutions to find
			var solution = routing.SolveWithParameters(searchParameters);


			// var distanceCallback = CalcCosting(manager, orders, cachedDistances);
			// var transitCallbackIndex = routing.RegisterTransitCallback(distanceCallback);
			// routing.SetArcCostEvaluatorOfAllVehicles(transitCallbackIndex);

			// AddConstraints(routing, manager, vehicles, transitCallbackIndex);

			// var strategy = SolutionStrategy();
			// var solution = routing.SolveWithParameters(strategy);

			//////////////////////////


			var avgSpeed = 30; // Average speed in km/h
			var shipments = new List<Shipment>();

			if (solution != null)
			{
				for (int i = 0; i < vehicles.Count; i++)
				{
					var route = new List<RoutePoint>();
					var index = routing.Start(i);
					double totalWeight = 0;
					double totalVolume = 0;
					double totalDistance = 0;
					int totalTime = 0;

					while (!routing.IsEnd(index))
					{
						var node = manager.IndexToNode(index);
						if (node > 0)
						{
							var order = orders[node - 1];
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
									StartTime = totalTime // Assign StartTime directly
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
								var fromAddress = orders[fromNode - 1].Address;
								var toAddress = orders[toNode - 1].Address;

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
									
									totalDistance += distance;
									totalTime += (int)((distance / avgSpeed) * 60); // Convert hours to minutes
								}
							}
						}

						index = nextIndex;
					}

					double vehicleMaxWeight =
						(_option?.Constraints.Weight == FillOption.Min ? vehicles[i].MinWeight :
						_option?.Constraints.Weight == FillOption.Recommended ? vehicles[i].RecommendedWeight :
						vehicles[i].MaxWeight) * 1; // Convert to kg
					double vehicleMaxVolume =
						(_option?.Constraints.Volume == FillOption.Min ? vehicles[i].MinVolume :
						_option?.Constraints.Volume == FillOption.Recommended ? vehicles[i].RecommendedVolume :
						vehicles[i].MaxVolume) * 1; // Convert to liters

					shipments.Add(new Shipment
					{
						IDVehicle = vehicles[i].Id,
						Route = route,
						TotalDistance = totalDistance,
						TotalTime = totalTime,
						TotalWeight = totalWeight,
						TotalVolume = totalVolume,
						WeightRate = totalWeight / vehicleMaxWeight,
						VolumeRate = totalVolume / vehicleMaxVolume
					});
				}
			}

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

		private LongLongToLong CalcCosting(RoutingIndexManager manager, List<DeliveryOrder> orders, List<IndexDistance> cachedDistances)
		{
			Console.WriteLine("Warning: ---------------------- CalcCosting ----------------------");
			return new LongLongToLong((fromIndex, toIndex) =>
			{
				Console.WriteLine("Warning: ---------------------- LongLongToLong ----------------------");
				//return 100;
				var fromNode = manager.IndexToNode((int)fromIndex);
				var toNode = manager.IndexToNode((int)toIndex);

				if (fromNode == 0 || toNode == 0)
				{
					return 0;
				}

				var fromAddress = orders[fromNode - 1].Address;
				var toAddress = orders[toNode - 1].Address;

				if (fromAddress == null || toAddress == null)
				{
					return long.MaxValue;
				}

				double totalCost = 0;

				foreach (var cost in _option?.Costs ?? new List<Cost>())
				{
					/*
					OrderType: Ưu tiên theo loại đơn hàng (e.g., giao hàng nhanh có thể có chi phí cao hơn).
					Weight: Trọng lượng đơn hàng ảnh hưởng đến chi phí vận chuyển.
					Volume: Thể tích đơn hàng ảnh hưởng đến chi phí vận chuyển.
					Cost: Chi phí giao hàng trực tiếp.
					Distance: Khoảng cách giao hàng ảnh hưởng đến chi phí nhiên liệu.
					FuelEfficiency: Hiệu quả tiêu thụ nhiên liệu (giảm chi phí).
					CO2Emission: Lượng khí thải CO2 (có thể được quy đổi thành chi phí môi trường).
					*/
					switch (cost.Type)
					{
						case "Distance":
							double distance = _distanceService.CalculateDistance(fromAddress, toAddress, ref cachedDistances);
							totalCost += distance * cost.Value;
							break;
						// case "OrderType":
						// 	double orderTypeCost = CalculateOrderTypeCost(fromOrder, toOrder);
						// 	totalCost += orderTypeCost * cost.Value;
						// 	break;
						// case "Weight":
						// 	double weightCost = CalculateWeightCost(fromOrder, toOrder);
						// 	totalCost += weightCost * cost.Value;
						// 	break;
						default:
							// Log a warning or handle unsupported cost types
							//Console.WriteLine($"Unsupported cost type: {cost.Type}");
							break;
					}
				}

				return (long)totalCost;
			});
		}

		private void AddConstraints(RoutingModel routing, RoutingIndexManager manager, List<Vehicle> vehicles, int transitCallbackIndex)
		{
			Console.WriteLine("Warning: ---------------------- AddConstraints ----------------------");
			foreach (var vehicle in vehicles)
			{
				if (_option?.Constraints.Weight != FillOption.None)
				{
					// Determine weight capacity based on CalcOption.Constraints.WeightOption
					int weightCapacity = _option?.Constraints.Weight switch
					{
						FillOption.Min => (int)(vehicle.MinWeight * 1000), // Minimum weight
						FillOption.Recommended => (int)(vehicle.RecommendedWeight * 1000), // Recommended weight
						FillOption.Max => (int)(vehicle.MaxWeight * 1000), // Maximum weight
						_ => int.MaxValue // No constraint (None)
					};

					// Add weight dimension
					routing.AddDimension(transitCallbackIndex, 0, weightCapacity, true, "Weight");
				}

				if (_option?.Constraints.Volume != FillOption.None)
				{
					// Determine volume capacity based on CalcOption.Constraints.VolumeOption
					int volumeCapacity = _option?.Constraints.Volume switch
					{
						FillOption.Min => (int)(vehicle.MinVolume * 1000), // Minimum volume
						FillOption.Recommended => (int)(vehicle.RecommendedVolume * 1000), // Recommended volume
						FillOption.Max => (int)(vehicle.MaxVolume * 1000), // Maximum volume	
						_ => int.MaxValue // No constraint (None)
					};

					// Add volume dimension
					routing.AddDimension(transitCallbackIndex, 0, volumeCapacity, true, "Volume");
				}
			}
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
