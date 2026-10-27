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
    public class JobTitleConfigController : Controller
    {
        private readonly ApplicationDbContext _context;

        public JobTitleConfigController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var configs = await _context.JobTitleConfigs.ToListAsync();
            
            if (!configs.Any())
            {
                var defaults = new[]
                {
                    new JobTitleConfig { JobTitle = "Giám đốc", Coefficient = 3.0m, Notes = "Mẫu" },
                    new JobTitleConfig { JobTitle = "Trưởng phòng", Coefficient = 2.0m, Notes = "Mẫu" },
                    new JobTitleConfig { JobTitle = "Phó phòng", Coefficient = 1.8m, Notes = "Mẫu" },
                    new JobTitleConfig { JobTitle = "Nhân viên", Coefficient = 1.0m, Notes = "Mẫu" },
                    new JobTitleConfig { JobTitle = "Thực tập sinh", Coefficient = 0.5m, Notes = "Mẫu" }
                };
                _context.JobTitleConfigs.AddRange(defaults);
                await _context.SaveChangesAsync();
                configs = defaults.ToList();
            }

            return View(configs);
        }

        [HttpPost]
        public async Task<IActionResult> SaveConfigs([FromBody] JobTitleConfig[] configs)
        {
            if (configs == null || configs.Length == 0) return BadRequest("Dữ liệu trống");

            foreach(var c in configs)
            {
                var existing = await _context.JobTitleConfigs.FindAsync(c.Id);
                if (existing != null)
                {
                    existing.Coefficient = c.Coefficient;
                    existing.Notes = c.Notes;
                }
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
