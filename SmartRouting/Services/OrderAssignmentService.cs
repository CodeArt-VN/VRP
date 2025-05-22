using System.Linq;
using Azure.Core;
using SmartRouting.Configurations;
using SmartRouting.Models;
using NetTopologySuite.Geometries; // Required for Point

namespace SmartRouting.Services
{
	public class OrderAssignmentService
	{
		public CalcOption? _option { get; set; }
		private readonly ApplicationDbContext _context;
		private readonly GeometryFactory _geometryFactory; 

		public OrderAssignmentService(ApplicationDbContext context, CalcOption? option = null)
		{
			_context = context ?? throw new ArgumentNullException(nameof(context));
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
			shipments = AssignOrdersToVehicles(vehicles, orders, iDDepotAddress);


			// 4. Return the list of Shipments and UnassignedOrders in RouteCalcResponse.
			return new RouteCalcResponse
			{
				Shipments = shipments,
				UnassignedOrders = unassignedOrders
			};
		}

		private List<Shipment> AssignOrdersToVehicles(List<Vehicle> vehicles, List<DeliveryOrder> orders, int iDDepotAddress)
		{
			// Đọc từ option.Priority để xác định thứ tự ưu tiên
			//    Group orders by priority

			//    Priority setting: Weight, Volume, Deadline, Distance....
			//    When assigning orders to vehicles, check the vehicle capacity (weight, volume) constraints by weightOption and volumeOption.

			// "OrderType",             // Ưu tiên theo loại đơn hàng (giao hàng nhanh, giao hàng tiết kiệm,...)
			// "Weight",                // Ưu tiên theo trọng lượng đơn hàng
			// "Volume",                // Ưu tiên theo thể tích đơn hàng
			// "Cost",                  // Ưu tiên theo chi phí giao hàng
			// "Distance",              // Ưu tiên theo khoảng cách giao hàng
			// "Time",                  // Ưu tiên theo thời gian giao hàng
			// "DeliveryWindow",        // Ưu tiên theo khung giờ giao hàng
			// "CustomerType",          // Ưu tiên theo loại khách hàng (VIP, thường,...)
			// "Fragility",             // Ưu tiên theo độ dễ vỡ của hàng hóa
			// "TrafficCondition",      // Ưu tiên theo điều kiện giao thông
			// "Weather",               // Ưu tiên theo điều kiện thời tiết
			// "DriverExperience",      // Ưu tiên theo kinh nghiệm tài xế
			// "RoadRestriction",       // Ưu tiên theo hạn chế tuyến đường
			// "FuelEfficiency",        // Ưu tiên theo hiệu quả tiêu thụ nhiên liệu
			// "CO2Emission"            // Ưu tiên theo lượng khí thải CO2

			var shipments = new List<Shipment>();








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



	}
}
