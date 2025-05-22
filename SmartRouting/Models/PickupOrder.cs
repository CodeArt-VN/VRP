using System.Collections.Generic;

namespace SmartRouting.Models
{
    public class PickupOrder
    {
        public int Id { get; set; }
        public int IDAddress { get; set; } // Updated from IDAddress
        public List<PickupItem>? Items { get; set; }
        public int DestinationDepotId { get; set; }
        public DateTime? Deadline { get; set; }
    }

    public class PickupItem
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int Quantity { get; set; }
        public double Weight { get; set; }
        public double Volume { get; set; }
    }
}