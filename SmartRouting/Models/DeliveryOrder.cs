using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using SmartRouting.Models;
using System.Linq;

namespace SmartRouting.Models
{
    public class DeliveryOrder
    {
        public int Id { get; set; }
        public int IDAddress { get; set; } // Updated from IDAddress
        public string? DeliveryType { get; set; }
        public List<OrderLine>? OrderLines { get; set; }
        public DateTime? Deadline { get; set; }
        public string? Priority { get; set; }
     
        // Navigation property for Address
        [ForeignKey("IDAddress")]
        public Address? Address { get; set; }
        public decimal Volume { get; set; }
        public decimal Weight { get; set; }
    }

    public class OrderLine
    {
        public int Id { get; set; }
        public string? Item { get; set; }
        public decimal Quantity { get; set; }
        public decimal Weight { get; set; }
        public decimal Volume { get; set; }
    }
}