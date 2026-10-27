using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public class AdminCategoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly Microsoft.Extensions.Logging.ILogger<AdminCategoryController> _logger;

        public AdminCategoryController(ApplicationDbContext context, Microsoft.Extensions.Logging.ILogger<AdminCategoryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IActionResult Index()
        {
            // Lấy thông tin thống kê tổng quan (Summary)
            ViewBag.TotalJobPositions = _context.JobPositions.Count();
            ViewBag.TotalJobTitles = _context.JobTitleConfigs.Count();
            ViewBag.TotalExperiences = _context.ExperienceConfigs.Count();
            ViewBag.TotalKPIs = _context.KPIConfigs.Count();
            ViewBag.TotalSalaryGrades = _context.SalaryGradeConfigs.Count();

            return View();
        }

        // --- API GET DATA ---
        [HttpGet]
        public async Task<IActionResult> GetData(string category)
        {
            try
            {
                object data = null;
                switch (category)
                {
                    case "JobPosition":
                        data = await _context.JobPositions.OrderBy(x => x.Group).ThenBy(x => x.Name).ToListAsync();
                        break;
                    case "JobTitle":
                        data = await _context.JobTitleConfigs.OrderBy(x => x.JobTitle).ToListAsync();
                        break;
                    case "Experience":
                        data = await _context.ExperienceConfigs.OrderBy(x => x.MinYears).ToListAsync();
                        break;
                    case "KPI":
                        data = await _context.KPIConfigs.OrderBy(x => x.KPIRating).ToListAsync();
                        break;
                    case "SalaryGrade":
                        data = await _context.SalaryGradeConfigs.OrderBy(x => x.SalaryRank).ToListAsync();
                        break;
                    default:
                        return BadRequest("Invalid category");
                }
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy dữ liệu danh mục");
                return Json(new { success = false, message = "Đã xảy ra lỗi khi xử lý yêu cầu. Vui lòng thử lại." });
            }
        }

        // --- API DELETE ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRecord(string category, int id)
        {
            try
            {
                switch (category)
                {
                    case "JobPosition":
                        var jp = await _context.JobPositions.FindAsync(id);
                        if (jp != null) { _context.JobPositions.Remove(jp); }
                        break;
                    case "JobTitle":
                        var jt = await _context.JobTitleConfigs.FindAsync(id);
                        if (jt != null) { _context.JobTitleConfigs.Remove(jt); }
                        break;
                    case "Experience":
                        var exp = await _context.ExperienceConfigs.FindAsync(id);
                        if (exp != null) { _context.ExperienceConfigs.Remove(exp); }
                        break;
                    case "KPI":
                        var kpi = await _context.KPIConfigs.FindAsync(id);
                        if (kpi != null) { _context.KPIConfigs.Remove(kpi); }
                        break;
                    case "SalaryGrade":
                        var sg = await _context.SalaryGradeConfigs.FindAsync(id);
                        if (sg != null) { _context.SalaryGradeConfigs.Remove(sg); }
                        break;
                    default:
                        return Json(new { success = false, message = "Invalid category" });
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (DbUpdateException)
            {
                return Json(new { success = false, message = "Không thể xóa dữ liệu vì đang được sử dụng ở nơi khác (ràng buộc khóa ngoại)." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa dữ liệu danh mục");
                return Json(new { success = false, message = "Đã xảy ra lỗi khi xử lý yêu cầu. Vui lòng thử lại." });
            }
        }

        // --- API SAVE (CREATE/UPDATE) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveJobPosition([FromBody] JobPosition model)
        {
            if (!ModelState.IsValid) return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
            try {
                if (model.Id > 0) _context.JobPositions.Update(model);
                else _context.JobPositions.Add(model);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            } catch(Exception ex) { 
                _logger.LogError(ex, "Lỗi khi lưu JobPosition");
                return Json(new { success = false, message = "Đã xảy ra lỗi khi xử lý yêu cầu. Vui lòng thử lại." }); 
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveJobTitle([FromBody] JobTitleConfig model)
        {
            if (!ModelState.IsValid) return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
            try {
                if (model.Id > 0) _context.JobTitleConfigs.Update(model);
                else _context.JobTitleConfigs.Add(model);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            } catch(Exception ex) { 
                _logger.LogError(ex, "Lỗi khi lưu JobTitle");
                return Json(new { success = false, message = "Đã xảy ra lỗi khi xử lý yêu cầu. Vui lòng thử lại." }); 
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveExperience([FromBody] ExperienceConfig model)
        {
            if (!ModelState.IsValid) return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
            try {
                if (model.Id > 0) _context.ExperienceConfigs.Update(model);
                else _context.ExperienceConfigs.Add(model);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            } catch(Exception ex) { 
                _logger.LogError(ex, "Lỗi khi lưu Experience");
                return Json(new { success = false, message = "Đã xảy ra lỗi khi xử lý yêu cầu. Vui lòng thử lại." }); 
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveKPI([FromBody] KPIConfig model)
        {
            if (!ModelState.IsValid) return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
            try {
                if (model.Id > 0) _context.KPIConfigs.Update(model);
                else _context.KPIConfigs.Add(model);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            } catch(Exception ex) { 
                _logger.LogError(ex, "Lỗi khi lưu KPI");
                return Json(new { success = false, message = "Đã xảy ra lỗi khi xử lý yêu cầu. Vui lòng thử lại." }); 
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSalaryGrade([FromBody] SalaryGradeConfig model)
        {
            if (!ModelState.IsValid) return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
            try {
                if (model.Id > 0) _context.SalaryGradeConfigs.Update(model);
                else _context.SalaryGradeConfigs.Add(model);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            } catch(Exception ex) { 
                _logger.LogError(ex, "Lỗi khi lưu SalaryGrade");
                return Json(new { success = false, message = "Đã xảy ra lỗi khi xử lý yêu cầu. Vui lòng thử lại." }); 
            }
        }
    }
}
