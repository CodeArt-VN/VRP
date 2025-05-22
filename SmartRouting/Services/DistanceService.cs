// SmartRouting/Services/DistanceService.cs
using Microsoft.Extensions.Configuration;
using NetTopologySuite.Geometries;
using SmartRouting.Models;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using SmartRouting.Configurations;

namespace SmartRouting.Services
{
	public class DistanceService // Removed : IDistanceService
	{
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly IConfiguration _configuration;
		private readonly GeometryFactory _geometryFactory;
		private readonly ApplicationDbContext _context;

		public DistanceService(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
		{
			_context = context ?? throw new ArgumentNullException(nameof(context));
			_httpClientFactory = httpClientFactory;
			_configuration = configuration;
			_geometryFactory = new GeometryFactory(new PrecisionModel(), 4326); // SRID for WGS 84
		}

		/// <summary>
		/// Calculates the distance between two geographical points.
		/// </summary>
		public double CalculateDistance(Address fromAddress, Address toAddress, List<IndexDistance> cachedDistances, double minHaversineDistance = 1000)
		{
			if (fromAddress == null || toAddress == null)
			{
				throw new ArgumentNullException("Addresses cannot be null for distance calculation.");
			}

			// If not found in cache, check if the addresses are the same
			if (fromAddress.Id == toAddress.Id)
			{
				return 0.0; // Distance is zero if both addresses are the same
			}

			// Check cached distances
			var cachedDistance = cachedDistances.FirstOrDefault(d =>
			(d.Loc1 == fromAddress.Id && d.Loc2 == toAddress.Id)
			|| (d.Loc1 == toAddress.Id && d.Loc2 == fromAddress.Id));
			if (cachedDistance != null && cachedDistance.Distance.HasValue)
			{
				// Return the cached distance if found
				return cachedDistance.Distance.Value;
			}

			double distance = 0.0;
			// Calculate distance using Haversine formula
			if (fromAddress.Location == null || toAddress.Location == null)
			{
				throw new ArgumentNullException("Address locations cannot be null.");
			}

			distance = CalculateHaversineDistance(fromAddress.Location, toAddress.Location);

			if (distance > minHaversineDistance)
				return distance;
			

			double? googleMapsDistance = GetGoogleMapsDistanceAsync(fromAddress, toAddress).Result;

			if (googleMapsDistance.HasValue)
				return googleMapsDistance.Value;
			else
				return distance;
		}

		public List<IndexDistance> GetIndicesDistance(List<int> AddressIds)
		{
			// Get cached distances from the database
			var cachedDistances = _context.IndexDistances
				.Where(d => AddressIds.Contains(d.Loc1) && AddressIds.Contains(d.Loc2))
				.ToList();

			return cachedDistances;
		}

		public double CalculateHaversineDistance(Point loc1, Point loc2)
		{
			if (loc1 == null || loc2 == null)
			{
				throw new ArgumentNullException("Locations cannot be null for Haversine calculation.");
			}
			return CalculateHaversineDistance(loc1.Y, loc1.X, loc2.Y, loc2.X);
		}
		public double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
		{
			const double R = 6371000; // Radius of Earth in meters
			var dLat = ToRadians(lat2 - lat1);
			var dLon = ToRadians(lon2 - lon1);
			lat1 = ToRadians(lat1);
			lat2 = ToRadians(lat2);

			var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
					Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1) * Math.Cos(lat2);
			var c = 2 * Math.Asin(Math.Sqrt(a));
			return R * c;
		}

		private static double ToRadians(double angle)
		{
			return Math.PI * angle / 180.0;
		}

		public async Task<double?> GetGoogleMapsDistanceAsync(Address originAddress, Address destinationAddress)
		{
			if (originAddress?.Location == null || destinationAddress?.Location == null)
			{
				// Or handle this case as per your application's requirements
				// For example, log a warning and return null, or throw an ArgumentNullException
				Console.WriteLine("Warning: Origin or destination address or their locations are null. Cannot calculate Google Maps distance.");
				return null;
			}
			double? result = await GetGoogleMapsDistanceAsync(originAddress.Location.Y, originAddress.Location.X, destinationAddress.Location.Y, destinationAddress.Location.X);

			// Ghi cache distance to database
			if (result != null) PostDistanceToDatabase(originAddress.Id, destinationAddress.Id, result.Value);


			return result;
		}

		private void PostDistanceToDatabase(int id1, int id2, double result)
		{
			// Check if the distance already exists in the database
			var existingDistance = _context.IndexDistances
				.FirstOrDefault(d => (d.Loc1 == id1 && d.Loc2 == id2) || (d.Loc1 == id2 && d.Loc2 == id1));

			if (existingDistance != null)
			{
				// Update the existing distance
				existingDistance.Distance = result;
				_context.IndexDistances.Update(existingDistance);
			}
			else
			{
				// Create a new distance entry
				var newDistance = new IndexDistance
				{
					Loc1 = id1,
					Loc2 = id2,
					Distance = result
				};
				_context.IndexDistances.Add(newDistance);
			}

			_context.SaveChanges();
		}

		public async Task<double?> GetGoogleMapsDistanceAsync(double originLat, double originLon, double destinationLat, double destinationLon)
		{
			var apiKey = _configuration["GoogleMaps:ApiKey"];
			if (string.IsNullOrEmpty(apiKey))
			{
				// Log this error or handle it appropriately
				// For now, let's throw an exception or return a specific error indicator
				throw new InvalidOperationException("Google Maps API key is not configured.");
			}

			var client = _httpClientFactory.CreateClient("GoogleMaps");
			// Ensure the culture is set to something that uses '.' as a decimal separator for the URL
			var requestUri = $"https://maps.googleapis.com/maps/api/distancematrix/json?origins={originLat.ToString(System.Globalization.CultureInfo.InvariantCulture)},{originLon.ToString(System.Globalization.CultureInfo.InvariantCulture)}&destinations={destinationLat.ToString(System.Globalization.CultureInfo.InvariantCulture)},{destinationLon.ToString(System.Globalization.CultureInfo.InvariantCulture)}&key={apiKey}&units=metric";

			try
			{
				var response = await client.GetAsync(requestUri);
				response.EnsureSuccessStatusCode(); // Throws if HTTP response status is an error code

				var jsonResponse = await response.Content.ReadAsStringAsync();
				// Consider using a more robust JSON parsing approach, perhaps with defined DTOs for the Google Maps response
				using (var document = JsonDocument.Parse(jsonResponse))
				{
					var root = document.RootElement;
					if (root.TryGetProperty("rows", out var rows) && rows.GetArrayLength() > 0)
					{
						if (rows[0].TryGetProperty("elements", out var elements) && elements.GetArrayLength() > 0)
						{
							if (elements[0].TryGetProperty("status", out var status) && status.GetString() == "OK")
							{
								if (elements[0].TryGetProperty("distance", out var distanceElement) &&
									distanceElement.TryGetProperty("value", out var valueElement) &&
									valueElement.TryGetInt32(out var distanceInMeters))
								{
									return distanceInMeters;
								}
							}
							else
							{
								// Log the status if not OK (e.g., "ZERO_RESULTS")
								Console.WriteLine($"Google Maps API returned status: {status.GetString()} for the request.");
								return null;
							}
						}
					}
				}
			}
			catch (HttpRequestException e)
			{
				// Log HttpRequestException (e.g., network error, DNS failure)
				Console.WriteLine($"Request error: {e.Message}");
				return null; // Or rethrow, or handle as per your error policy
			}
			catch (JsonException e)
			{
				// Log JsonException (e.g., malformed JSON response)
				Console.WriteLine($"JSON parsing error: {e.Message}");
				return null; // Or rethrow, or handle as per your error policy
			}
			catch (Exception e) // Catch-all for other unexpected errors
			{
				Console.WriteLine($"An unexpected error occurred: {e.Message}");
				return null; // Or rethrow
			}

			return null; // Default return if distance cannot be determined
		}


	}
}
