using Microsoft.AspNetCore.Mvc;
using SmartRouting.Models;
using SmartRouting.Services;
using SmartRouting.Configurations;

namespace SmartRouting.Controllers
{
    [ApiController]
    [Route("api/Routes")]
    public class RoutesController : ControllerBase
    {
		private readonly ApplicationDbContext _context;
        private readonly ILogger<RoutesController> _logger;



		public RoutesController(ApplicationDbContext context, ILogger<RoutesController> logger)
		{
			_context = context;
			_logger = logger;
		}

        // POST: api/Routes/Calc
        [HttpPost("Calc")]
        public IActionResult RoutesCalc([FromBody] RouteCalcRequest request)
        {
            try
            {
                if (request.Vehicles == null || request.Orders == null)
                {
                    _logger.LogWarning("Invalid request: Vehicles or Orders are null.");
                    return BadRequest("Vehicles and Orders cannot be null.");
                }

				OrderAssignmentService orderAssignmentService = new OrderAssignmentService(_context, request.Option);
				RouteCalcResponse response = orderAssignmentService.CalculateRoutes(request.Vehicles, request.Orders, request.IDDepotAddress);

                _logger.LogInformation("Routes calculated successfully.");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while calculating routes.");
                return StatusCode(500, "An error occurred while processing your request.");
            }
        }
    }
}