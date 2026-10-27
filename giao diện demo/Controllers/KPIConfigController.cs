using Microsoft.AspNetCore.Mvc;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.HROnly)]
    [AutoValidateAntiforgeryToken]
    public class KPIConfigController : Controller
    {
        private readonly ApplicationDbContext _context;

        public KPIConfigController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var configs = await _context.KPIConfigs.OrderBy(x => x.KPIRating).ToListAsync();
            
            if (!configs.Any())
            {
                var defaults = new[]
                {
                    new KPIConfig { KPIRating = "A", Coefficient = 1.5m, Notes = "Xuất sắc" },
                    new KPIConfig { KPIRating = "B", Coefficient = 1.2m, Notes = "Tốt" },
                    new KPIConfig { KPIRating = "C", Coefficient = 1.0m, Notes = "Đạt" },
                    new KPIConfig { KPIRating = "D", Coefficient = 0.8m, Notes = "Chưa đạt" }
                };
                _context.KPIConfigs.AddRange(defaults);
                await _context.SaveChangesAsync();
                configs = defaults.ToList();
            }

            return View(configs);
        }

        [HttpPost]
        public async Task<IActionResult> SaveConfigs([FromBody] KPIConfig[] configs)
        {
            if (configs == null || configs.Length == 0) return BadRequest("Dữ liệu trống");

            foreach(var c in configs)
            {
                var existing = await _context.KPIConfigs.FindAsync(c.Id);
                if (existing != null)
                {
                    existing.KPIRating = c.KPIRating;
                    existing.Coefficient = c.Coefficient;
                    existing.Notes = c.Notes;
                }
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
