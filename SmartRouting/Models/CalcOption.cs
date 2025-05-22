using System.Collections.Generic;

namespace SmartRouting.Models
{
	public class CalcOption
	{
		public List<string> Priority { get; set; } = new List<string>(); // Danh sách ưu tiên: "Cost", "Weight", "Volume"
		public FillOption WeightOption { get; set; } = FillOption.Recommended; // Tùy chọn trọng tải
		public FillOption VolumeOption { get; set; } = FillOption.Recommended; // Tùy chọn thể tích

	}


	public enum FillOption
	{
		None,
		Min,
		Max,
		Recommended
	}
	

}
