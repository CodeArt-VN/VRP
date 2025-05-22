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
        public List<RoutePoint> Route { get; set; } = new List<RoutePoint>();
        public double TotalDistance { get; set; } // Added for total distance of the route
        public int TotalTime { get; set; } // Changed from string to int (minutes)
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
    }
}