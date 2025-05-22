using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartRouting.Configurations;
using SmartRouting.Models;
using System.Linq;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries; // Required for Point

namespace SmartRouting.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AddressController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AddressController> _logger;
        private readonly GeometryFactory _geometryFactory; // To create Point objects

        public AddressController(ApplicationDbContext context, ILogger<AddressController> logger)
        {
            _context = context;
            _logger = logger;
            _geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326); // Initialize with SRID 4326
        }

        // GET: api/Address
        [HttpGet]
        public async Task<IActionResult> GetAddresses([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1 || pageSize < 1)
            {
                return BadRequest("Page and pageSize must be greater than 0.");
            }

            var query = _context.Addresses.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a => (a.Name != null && a.Name.Contains(search)) ||
                                         (a.District != null && a.District.Contains(search)) ||
                                         (a.Province != null && a.Province.Contains(search)) ||
                                         (a.Ward != null && a.Ward.Contains(search)) ||
                                         (a.Street != null && a.Street.Contains(search)));
            }

            var totalItems = await query.CountAsync();
            var addresses = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new
            {
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize,
                Items = addresses
            };

            return Ok(result);
        }

        // GET: api/Address/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetAddress(int id)
        {
            var address = await _context.Addresses.FindAsync(id);
            if (address == null)
            {
                return NotFound();
            }
            return Ok(address);
        }

        // POST: api/Address - This will handle POST api/Address for a list of Address objects
        [HttpPost]
        public async Task<IActionResult> CreateAddresses([FromBody] List<AddressInputModel> addressInputs) // Changed to use AddressInputModel
        {
            Request.EnableBuffering();
            string rawRequestBody = string.Empty;
            using (var reader = new System.IO.StreamReader(Request.Body, encoding: System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
            {
                rawRequestBody = await reader.ReadToEndAsync();
                _logger.LogInformation("Raw request body for CreateAddresses: {RequestBody}", rawRequestBody);
                Request.Body.Position = 0;
            }

            if (addressInputs == null || !addressInputs.Any())
            {
                return BadRequest("Address list cannot be null or empty.");
            }

            var postedIds = new List<int>();
            var existsIds = new List<int>();
            var addressesToAdd = new List<Address>();

            foreach (var inputAddress in addressInputs)
            {
                if (inputAddress == null)
                {
                    _logger.LogWarning("Null address input object found in the batch request.");
                    continue;
                }

                if (await _context.Addresses.AnyAsync(a => a.Id == inputAddress.Id))
                {
                    existsIds.Add(inputAddress.Id);
                }
                else
                {
                    var newAddress = new Address
                    {
                        Id = inputAddress.Id,
                        Name = inputAddress.Name,
                        Phone = inputAddress.Phone,
                        District = inputAddress.District,
                        Province = inputAddress.Province,
                        Ward = inputAddress.Ward,
                        Street = inputAddress.Street,
                        Address1 = inputAddress.Address1
                    };

                    if (inputAddress.Latitude.HasValue && inputAddress.Longitude.HasValue)
                    {
                        newAddress.Location = _geometryFactory.CreatePoint(new Coordinate(inputAddress.Longitude.Value, inputAddress.Latitude.Value));
                    }
                    else
                    {
                        newAddress.Location = null; // Or handle as an error if coordinates are mandatory
                    }
                    addressesToAdd.Add(newAddress);
                }
            }

            if (addressesToAdd.Any())
            {
                _context.Addresses.AddRange(addressesToAdd);
                await _context.SaveChangesAsync();
                postedIds.AddRange(addressesToAdd.Select(a => a.Id));
            }

            return Ok(new { ExistsIds = existsIds, PostedIds = postedIds });
        }

        // PUT: api/Address/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAddress(int id, [FromBody] AddressInputModel addressInput) // Changed to use AddressInputModel
        {
            if (id != addressInput.Id)
            {
                return BadRequest("Address ID mismatch.");
            }

            // ModelState validation might need adjustment if AddressInputModel has different validation attributes
            // than Address model. For simplicity, assuming basic validation or custom validation here.

            var addressToUpdate = await _context.Addresses.FindAsync(id);
            if (addressToUpdate == null)
            {
                return NotFound();
            }

            // Manual mapping from AddressInputModel to Address entity
            addressToUpdate.Name = addressInput.Name;
            addressToUpdate.Phone = addressInput.Phone;
            addressToUpdate.District = addressInput.District;
            addressToUpdate.Province = addressInput.Province;
            addressToUpdate.Ward = addressInput.Ward;
            addressToUpdate.Street = addressInput.Street;
            addressToUpdate.Address1 = addressInput.Address1;

            if (addressInput.Latitude.HasValue && addressInput.Longitude.HasValue)
            {
                addressToUpdate.Location = _geometryFactory.CreatePoint(new Coordinate(addressInput.Longitude.Value, addressInput.Latitude.Value));
            }
            else
            {
                addressToUpdate.Location = null; // Or handle as an error/ignore if coordinates are not provided for update
            }

            _context.Entry(addressToUpdate).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AddressExists(id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }

        // PATCH: api/Address/{id}
        [HttpPatch("{id}")]
        public async Task<IActionResult> PatchAddress(int id, [FromBody] AddressInputModel patchData) // Changed to accept AddressInputModel
        {
            if (patchData == null)
            {
                return BadRequest("Patch data cannot be null.");
            }

            // Check if Id is provided in the body and if it matches the route Id
            if (patchData.Id != 0 && patchData.Id != id)
            {
                return BadRequest("Address ID in request body does not match ID in route.");
            }

            var addressEntity = await _context.Addresses.FindAsync(id);
            if (addressEntity == null)
            {
                return NotFound($"Address with ID {id} not found.");
            }

            bool changed = false;

            // Update properties if they are provided in the patchData
            // For AddressInputModel, Id is present but not used for patching the entity itself,
            // as 'id' from the route is authoritative.
            // We could add a check: if (patchData.Id != 0 && patchData.Id != id) return BadRequest("ID in body does not match ID in route.");

            if (patchData.Name != null)
            {
                addressEntity.Name = patchData.Name;
                changed = true;
            }
            if (patchData.Phone != null)
            {
                addressEntity.Phone = patchData.Phone;
                changed = true;
            }
            if (patchData.District != null)
            {
                addressEntity.District = patchData.District;
                changed = true;
            }
            if (patchData.Province != null)
            {
                addressEntity.Province = patchData.Province;
                changed = true;
            }
            if (patchData.Ward != null)
            {
                addressEntity.Ward = patchData.Ward;
                changed = true;
            }
            if (patchData.Street != null)
            {
                addressEntity.Street = patchData.Street;
                changed = true;
            }
            if (patchData.Address1 != null)
            {
                addressEntity.Address1 = patchData.Address1;
                changed = true;
            }

            // Handle Location update
            if (patchData.Latitude.HasValue && patchData.Longitude.HasValue)
            {
                addressEntity.Location = _geometryFactory.CreatePoint(new Coordinate(patchData.Longitude.Value, patchData.Latitude.Value));
                changed = true;
            }
            // Optional: Allow explicitly setting Location to null if Latitude and Longitude are null in the DTO
            // else if (patchData.Latitude == null && patchData.Longitude == null)
            // {
            //     // This condition means client sent { "latitude": null, "longitude": null }
            //     // You need to decide if this means "clear the location" or "ignore if not both are present".
            //     // If you want to clear it:
            //     // if (addressEntity.Location != null) // only mark as changed if it was previously set
            //     // {
            //     //     addressEntity.Location = null;
            //     //     changed = true;
            //     // }
            // }


            if (!changed)
            {
                return Ok(new { Message = "No changes detected in the provided data.", Address = addressEntity });
            }

            _context.Entry(addressEntity).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AddressExists(id))
                {
                    return NotFound($"Concurrency error: Address with ID {id} not found.");
                }
                throw;
            }

            return Ok(addressEntity); // Return the updated address or NoContent()
        }

        // DELETE: api/Address/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAddress(int id)
        {
            var address = await _context.Addresses.FindAsync(id);
            if (address == null)
            {
                return NotFound();
            }

            _context.Addresses.Remove(address);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private bool AddressExists(int id)
        {
            return _context.Addresses.Any(e => e.Id == id);
        }
    }

    // DTO for POST and PUT requests to handle Latitude/Longitude input
    public class AddressInputModel
    {
        public int Id { get; set; } // Keep Id for POST (batch) and PUT
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? District { get; set; }
        public string? Province { get; set; }
        public string? Ward { get; set; }
        public string? Street { get; set; }
        public string? Address1 { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}