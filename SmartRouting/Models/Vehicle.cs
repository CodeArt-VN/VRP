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

		public double VolumeMin { get; set; }
		public double VolumeRecommended { get; set; }
		public double VolumeMax { get; set; }
		public double VolumeRemaining { get; set; }

	public double WeightMin { get; set; }
	public double WeightRecommended { get; set; }
	public double WeightMax { get; set; }
	public double WeightRemaining { get; set; }

		public string? OperatingArea { get; set; }
		public string? RestrictedRoutes { get; set; }
		public string? CurrentStatus { get; set; }
		public Point? CurrentLocation { get; set; } // Added for GEOGRAPHY type
	}
}