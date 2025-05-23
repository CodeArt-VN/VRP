using System.Collections.Generic;

namespace SmartRouting.Models
{
	public class CalcOption
	{
		public List<Cost> Costs { get; set; } = new List<Cost>();
		public Constraint Constraints { get; set; } = new Constraint();
		public string SolutionStrategy { get; set; } = "SAVINGS"; //CHEAPEST, SAVINGS, SWEEP
		

	}

	public class Constraint
	{
		public FillOption Weight { get; set; } = FillOption.Recommended; 
		public FillOption Volume { get; set; } = FillOption.Recommended; 
	}

	//Cost per 1Km distance
	public class Cost
	{
		public string Type { get; set; } = "Distance"; // e.g., "Distance", "Time", etc.
		public double Value { get; set; } = 0.0; // Cost per unit (e.g., per km)

	}


	public enum FillOption
	{
		None,
		Min,
		Max,
		Recommended
	}
}
