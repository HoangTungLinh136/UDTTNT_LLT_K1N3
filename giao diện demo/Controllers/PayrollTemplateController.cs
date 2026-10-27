using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Authorization;
using giao_dien_demo.Helpers;
using giao_dien_demo.Models;
using System.Linq;
using System.Threading.Tasks;
using giao_dien_demo.Data;
using System.Text.Json;
using System.Collections.Generic;
using System;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.HROnly)]
    [AutoValidateAntiforgeryToken]
    public class PayrollTemplateController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.IPayrollCalculationService _payrollService;

        public PayrollTemplateController(ApplicationDbContext context, giao_dien_demo.Services.IPayrollCalculationService payrollService)
        {
            _context = context;
            _payrollService = payrollService;
        }

        private bool CheckLogin()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("User"));
        }

        private bool CheckPermission()
        {
            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            return role == "HR";
        }

        public IActionResult Index()
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            if (!CheckPermission()) return Forbid();

            return View();
        }

        [HttpGet]
        public IActionResult GetTemplates()
        {
            if (!CheckLogin() || !CheckPermission()) return Unauthorized();
            var templates = PayrollTemplateService.GetAllTemplates().OrderByDescending(t => t.IsDefault).ThenByDescending(t => t.UpdatedAt).ToList();
            return Json(templates);
        }

        [HttpGet]
        public IActionResult GetTemplate(string id)
        {
            if (!CheckLogin() || !CheckPermission()) return Unauthorized();
            var template = PayrollTemplateService.GetTemplate(id);
            if (template == null) return NotFound();
            return Json(template);
        }

        [HttpGet]
        public IActionResult GetAllowedFields()
        {
            if (!CheckLogin() || !CheckPermission()) return Unauthorized();
            return Json(PayrollTemplateService.GetAllowedFields());
        }

        [HttpPost]
        public IActionResult SaveTemplate([FromBody] PayrollTemplate template)
        {
            if (!CheckLogin() || !CheckPermission()) return Unauthorized();
            if (template == null || string.IsNullOrWhiteSpace(template.Name)) return BadRequest("Tên mẫu không được để trống.");
            
            var saved = PayrollTemplateService.SaveTemplate(template);
            return Json(new { success = true, data = saved });
        }

        [HttpPost]
        public IActionResult DuplicateTemplate(string id)
        {
            if (!CheckLogin() || !CheckPermission()) return Unauthorized();
            var dup = PayrollTemplateService.DuplicateTemplate(id);
            if (dup == null) return NotFound();
            
            return Json(new { success = true, data = dup });
        }

        [HttpPost]
        public IActionResult SetDefaultTemplate(string id)
        {
            if (!CheckLogin() || !CheckPermission()) return Unauthorized();
            PayrollTemplateService.SetDefault(id);
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetPreviewData()
        {
            if (!CheckLogin() || !CheckPermission()) return Unauthorized();

            // Tính nhanh lương kỳ hiện tại để làm dữ liệu preview
            var calculatedSalaries = await _payrollService.CalculatePayrollAsync(DateTime.Now);
            
            // Lấy 3 nhân viên đầu tiên làm Preview (Sanitized/Limited data)
            var previewData = calculatedSalaries.Take(3).ToList();
            
            // Convert to a dictionary list to easily map fieldKeys
            var resultList = new List<Dictionary<string, object>>();
            foreach (var s in previewData)
            {
                var dict = new Dictionary<string, object>
                {
                    { "EmployeeCode", "EMP" + (s.EmployeeId?.ToString("D3") ?? "000") },
                    { "EmployeeName", s.EmployeeName },
                    { "DepartmentName", s.DepartmentName },
                    { "SalaryMonth", s.SalaryMonth.ToString("MM/yyyy") },
                    { "BaseSalary", s.TotalSalary },
                    { "PerformanceSalary", s.PerformanceSalary },
                    { "AllowanceCost", s.AllowanceCost },
                    { "OvertimeCost", s.OvertimeCost },
                    { "Reward", s.Reward },
                    { "TotalSalary", s.TotalSalary }, // Note: TotalSalary in Salary model is TotalGross
                    { "Insurance", s.Insurance },
                    { "TaxTNCN", s.TaxTNCN },
                    { "NetSalary", s.NetSalary }
                };
                resultList.Add(dict);
            }
            
            // If empty, add a dummy row just for UI structure preview
            if (!resultList.Any())
            {
                resultList.Add(new Dictionary<string, object>
                {
                    { "EmployeeCode", "EMP000" },
                    { "EmployeeName", "Nguyễn Văn Preview" },
                    { "DepartmentName", "Phòng Mẫu" },
                    { "SalaryMonth", "09/2026" },
                    { "BaseSalary", 10000000 },
                    { "PerformanceSalary", 0 },
                    { "AllowanceCost", 500000 },
                    { "OvertimeCost", 200000 },
                    { "Reward", 0 },
                    { "TotalSalary", 10700000 },
                    { "Insurance", 1000000 },
                    { "TaxTNCN", 0 },
                    { "NetSalary", 9700000 }
                });
            }

            return Json(resultList);
        }
    }
}
