using NetTopologySuite.Geometries; // Added for Point

namespace SmartRouting.Models
{
	public class Vehicle
	{
		public int Id { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public decimal Length { get; set; }
		public decimal Width { get; set; }
		public decimal Height { get; set; }
		public string? VehicleType { get; set; }

		public decimal VolumeMin { get; set; }
		public decimal VolumeRecommended { get; set; }
		public decimal VolumeMax { get; set; }
		public decimal VolumeRemaining { get; set; }

	public decimal WeightMin { get; set; }
	public decimal WeightRecommended { get; set; }
	public decimal WeightMax { get; set; }
	public decimal WeightRemaining { get; set; }

		public string? OperatingArea { get; set; }
		public string? RestrictedRoutes { get; set; }
		public string? CurrentStatus { get; set; }
		public Point? CurrentLocation { get; set; } // Added for GEOGRAPHY type
	}
}