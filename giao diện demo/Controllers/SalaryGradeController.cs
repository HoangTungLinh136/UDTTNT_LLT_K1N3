using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.HROnly)]
    [AutoValidateAntiforgeryToken]
    public class SalaryGradeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SalaryGradeController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool IsAdminOrHR()
        {
            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            return role == "HR";
        }

        public async Task<IActionResult> Index()
        {
            if (!IsAdminOrHR())
            {
                return RedirectToAction("Login", "Account");
            }
            
            var configs = await _context.SalaryGradeConfigs.OrderBy(x => x.SalaryRank).ToListAsync();
            
            // Seed default if empty
            if (!configs.Any())
            {
                var defaults = new[]
                {
                    new SalaryGradeConfig { AcademicLevel = "Phổ thông", SalaryRank = 1, Coefficient = 0, SuggestedAllowance = 0, Notes = "Dữ liệu mẫu (Vui lòng cấu hình)" },
                    new SalaryGradeConfig { AcademicLevel = "Trung cấp", SalaryRank = 2, Coefficient = 0, SuggestedAllowance = 0, Notes = "Dữ liệu mẫu (Vui lòng cấu hình)" },
                    new SalaryGradeConfig { AcademicLevel = "Cao đẳng", SalaryRank = 3, Coefficient = 0, SuggestedAllowance = 0, Notes = "Dữ liệu mẫu (Vui lòng cấu hình)" },
                    new SalaryGradeConfig { AcademicLevel = "Đại học", SalaryRank = 4, Coefficient = 0, SuggestedAllowance = 0, Notes = "Dữ liệu mẫu (Vui lòng cấu hình)" },
                    new SalaryGradeConfig { AcademicLevel = "Sau đại học", SalaryRank = 5, Coefficient = 0, SuggestedAllowance = 0, Notes = "Dữ liệu mẫu (Vui lòng cấu hình)" }
                };
                
                _context.SalaryGradeConfigs.AddRange(defaults);
                await _context.SaveChangesAsync();
                
                configs = defaults.ToList();
            }

            return View(configs);
        }

        [HttpPost]
        public async Task<IActionResult> SaveConfigs([FromBody] SalaryGradeConfig[] configs)
        {
            if (!IsAdminOrHR()) return Unauthorized();

            if (configs == null || configs.Length == 0) return BadRequest("Dữ liệu trống");

            foreach(var c in configs)
            {
                var existing = await _context.SalaryGradeConfigs.FindAsync(c.Id);
                if (existing != null)
                {
                    existing.Coefficient = c.Coefficient;
                    existing.SuggestedAllowance = c.SuggestedAllowance;
                    existing.Notes = c.Notes;
                    existing.SalaryRank = c.SalaryRank;
                }
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetSuggestedAllowance(string level)
        {
            if (string.IsNullOrEmpty(level)) return Json(new { allowance = 0 });
            var conf = await _context.SalaryGradeConfigs.FirstOrDefaultAsync(x => x.AcademicLevel == level);
            return Json(new { allowance = conf?.SuggestedAllowance ?? 0 });
        }
    }
}
