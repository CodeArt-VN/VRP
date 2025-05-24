using System.Collections.Generic;

namespace SmartRouting.Models
{
    public class RouteCalcResponse
    {
        public List<Shipment>? Shipments { get; set; }
        public List<UnassignedOrder>? UnassignedOrders { get; set; }
    }

	public class Shipment
	{
		public int IDVehicle { get; set; }
		public int Trip { get; set; } // Added for trip number
		public List<RoutePoint> Route { get; set; } = new List<RoutePoint>();
		public double TotalDistance { get; set; } // Added for total distance of the route
		public int TotalTime { get; set; } // Changed from string to int (minutes)
		public double TotalWeight { get; set; } // Added for total weight of the shipment
		public double TotalVolume { get; set; } // Added for total volume of the shipment
		public double TotalCost { get; set; } // Added for total cost of the shipment

		// Added properties for capacity rate
		public double WeightRate { get; set; } // Weight rate (0-1)
		public double VolumeRate { get; set; } // Volume rate (0-1)

    }

    public class UnassignedOrder
    {
        public int IDOrder { get; set; } // Updated from IDOrder
        public string Reason { get; set; } = string.Empty;
    }

	public class RoutePoint
	{
		public int IDAddress { get; set; } // Address ID for the route point
		public int IDOrder { get; set; } // Added to replace IDOrder
		public int Sequence { get; set; } // Sequence number in the route
		public double Latitude { get; set; } // Latitude for map marker
		public double Longitude { get; set; } // Longitude for map marker
		public int StartTime { get; set; } // Start time in minutes
    }
}