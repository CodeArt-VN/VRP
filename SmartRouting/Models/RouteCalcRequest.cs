using System.Collections.Generic;
using SmartRouting.Models; // Thêm namespace để tham chiếu đến CalcOption

namespace SmartRouting.Models
{
    public class RouteCalcRequest
    {
        public List<Vehicle>? Vehicles { get; set; }
        public List<DeliveryOrder>? Orders { get; set; }
        public int IDDepotAddress { get; set; }
        public CalcOption? Option { get; set; }
    }
}