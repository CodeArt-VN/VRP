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
        public decimal Quantity { get; set; }
        public decimal Weight { get; set; }
        public decimal Volume { get; set; }
    }
}