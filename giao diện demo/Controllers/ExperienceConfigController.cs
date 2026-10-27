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
    public class ExperienceConfigController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ExperienceConfigController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var configs = await _context.ExperienceConfigs.OrderBy(x => x.MinYears).ToListAsync();
            
            if (!configs.Any())
            {
                var defaults = new[]
                {
                    new ExperienceConfig { MinYears = 0, MaxYears = 1, Coefficient = 1.0m, Notes = "Dưới 1 năm" },
                    new ExperienceConfig { MinYears = 2, MaxYears = 3, Coefficient = 1.2m, Notes = "1 - 3 năm" },
                    new ExperienceConfig { MinYears = 4, MaxYears = 5, Coefficient = 1.5m, Notes = "4 - 5 năm" },
                    new ExperienceConfig { MinYears = 6, MaxYears = 99, Coefficient = 2.0m, Notes = "Trên 5 năm" }
                };
                _context.ExperienceConfigs.AddRange(defaults);
                await _context.SaveChangesAsync();
                configs = defaults.ToList();
            }

            return View(configs);
        }

        [HttpPost]
        public async Task<IActionResult> SaveConfigs([FromBody] ExperienceConfig[] configs)
        {
            if (configs == null || configs.Length == 0) return BadRequest("Dữ liệu trống");

            foreach(var c in configs)
            {
                var existing = await _context.ExperienceConfigs.FindAsync(c.Id);
                if (existing != null)
                {
                    existing.MinYears = c.MinYears;
                    existing.MaxYears = c.MaxYears;
                    existing.Coefficient = c.Coefficient;
                    existing.Notes = c.Notes;
                }
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
