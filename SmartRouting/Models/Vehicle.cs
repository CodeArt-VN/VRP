using NetTopologySuite.Geometries; // Added for Point

namespace SmartRouting.Models
{
	public class Vehicle
	{
		public int Id { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public double Length { get; set; }
		public double Width { get; set; }
		public double Height { get; set; }
		public string? VehicleType { get; set; }
		public double MinVolume { get; set; }
		public double RecommendedVolume { get; set; }
		public double MaxVolume { get; set; }
		public double MinWeight { get; set; }
		public double RecommendedWeight { get; set; }
		public double MaxWeight { get; set; }
		public string? OperatingArea { get; set; }
		public string? RestrictedRoutes { get; set; }
		public string? CurrentStatus { get; set; }
		public double? RemainingWeight { get; set; }
		public double? RemainingVolume { get; set; }
		public Point? CurrentLocation { get; set; } // Added for GEOGRAPHY type
	}
}