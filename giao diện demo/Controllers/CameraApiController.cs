using Microsoft.AspNetCore.Mvc;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public class CameraApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CameraApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        public class DetectionEventModel
        {
            public string ZoneName { get; set; } = string.Empty;
        }

        [HttpPost("log-detection")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogDetection([FromBody] DetectionEventModel model)
        {
            var config = await _context.CameraConfigs.FirstOrDefaultAsync();
            if (config == null)
            {
                return BadRequest("Camera is not configured.");
            }

            // Simple rate limiting: don't log more than one event every 10 seconds for the same zone
            var recentEvent = await _context.CameraEvents
                .Where(e => e.ZoneName == model.ZoneName && e.Timestamp > DateTime.Now.AddSeconds(-10))
                .FirstOrDefaultAsync();

            if (recentEvent == null)
            {
                var ev = new CameraEvent
                {
                    CameraConfigId = config.Id,
                    ZoneName = model.ZoneName,
                    Timestamp = DateTime.Now
                };
                _context.CameraEvents.Add(ev);
                await _context.SaveChangesAsync();
                
                // TODO: In a complete implementation, we'd trigger a SignalR toast notification here
            }

            return Ok(new { success = true });
        }

        public class PtzModel
        {
            public string Command { get; set; } = string.Empty;
        }

        [HttpPost("ptz")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PtzControl([FromBody] PtzModel model)
        {
            var config = await _context.CameraConfigs.FirstOrDefaultAsync();
            if (config == null) return BadRequest("Not configured");

            return StatusCode(StatusCodes.Status501NotImplemented, new
            {
                success = false,
                message = "PTZ control is not supported by the configured camera integration."
            });
        }
    }
}
