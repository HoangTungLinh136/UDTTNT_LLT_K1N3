using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    public class HolidayController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HolidayController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Authorize(Roles = $"{AppRoles.HR},{AppRoles.Leader}")]
        public IActionResult Index(int? year, string scope = "", string status = "")
        {

            int currentYear = DateTime.Now.Year;
            int selectedYear = year ?? currentYear;

            var query = _context.Holidays.AsQueryable();

            // Filter by Year (check if FromDate or ToDate falls in the selected year)
            query = query.Where(h => h.FromDate.Year == selectedYear || h.ToDate.Year == selectedYear);

            if (!string.IsNullOrEmpty(scope))
            {
                query = query.Where(h => h.ScopeType == scope);
            }
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(h => h.Status == status);
            }

            var holidays = query.OrderBy(h => h.FromDate).ToList();

            // KPI is calculated based on ALL holidays in that year, regardless of other filters (UX decision)
            var yearHolidays = _context.Holidays.Where(h => h.FromDate.Year == selectedYear || h.ToDate.Year == selectedYear).ToList();
            
            var vm = new HolidayViewModel
            {
                Holidays = holidays,
                TotalHolidays = yearHolidays.Count,
                ActiveHolidays = yearHolidays.Count(h => h.Status == "ACTIVE"),
                InactiveHolidays = yearHolidays.Count(h => h.Status == "INACTIVE"),
                FilterYear = selectedYear,
                FilterScope = scope,
                FilterStatus = status
            };

            // Master data for filters & forms
            var years = new List<int>();
            for (int i = currentYear - 2; i <= currentYear + 2; i++)
            {
                years.Add(i);
            }
            vm.AvailableYears = years;

            var depts = _context.Employees.Where(e => !string.IsNullOrEmpty(e.Department)).Select(e => e.Department!).Distinct().ToList();
            vm.Departments = depts.Select(d => new SelectListItem { Value = d, Text = d }).ToList();

            var emps = _context.Employees.Select(e => new { e.EmployeeCode, e.Name }).ToList();
            vm.Employees = emps.Select(e => new SelectListItem { Value = e.EmployeeCode, Text = $"{e.EmployeeCode} - {e.Name}" }).ToList();

            return View(vm);
        }

        [HttpGet]
        [Authorize(Roles = $"{AppRoles.HR},{AppRoles.Leader}")]
        public IActionResult GetHoliday(int id)
        {
            var h = _context.Holidays.FirstOrDefault(x => x.Id == id);
            if (h == null) return NotFound();

            var dto = new HolidayDto
            {
                Id = h.Id,
                HolidayName = h.HolidayName,
                FromDate = h.FromDate.ToString("yyyy-MM-dd"),
                ToDate = h.ToDate.ToString("yyyy-MM-dd"),
                ScopeType = h.ScopeType,
                ScopeValue = h.ScopeValue,
                IsPaidHoliday = h.IsPaidHoliday,
                Status = h.Status,
                Notes = h.Notes ?? ""
            };

            return Json(dto);
        }

        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public IActionResult SaveHoliday([FromBody] HolidayDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.HolidayName)) return BadRequest("Tên ngày nghỉ không được để trống.");

            if (!DateTime.TryParse(dto.FromDate, out DateTime fromDate)) return BadRequest("Từ ngày không hợp lệ.");
            if (!DateTime.TryParse(dto.ToDate, out DateTime toDate)) return BadRequest("Đến ngày không hợp lệ.");

            if (fromDate > toDate) return BadRequest("Từ ngày không được lớn hơn Đến ngày.");

            // Overlap Validation
            // Find if any existing ACTIVE holiday overlaps with this date range, for the same scope
            var overlapping = _context.Holidays.FirstOrDefault(h => 
                h.Id != dto.Id &&
                h.Status == "ACTIVE" &&
                h.ScopeType == dto.ScopeType &&
                h.ScopeValue == dto.ScopeValue &&
                ((fromDate >= h.FromDate && fromDate <= h.ToDate) || 
                 (toDate >= h.FromDate && toDate <= h.ToDate) ||
                 (fromDate <= h.FromDate && toDate >= h.ToDate))
            );

            if (overlapping != null)
            {
                return BadRequest($"Khoảng thời gian này bị trùng với ngày lễ '{overlapping.HolidayName}' đang áp dụng.");
            }

            Holiday h;
            if (dto.Id > 0)
            {
                h = _context.Holidays.FirstOrDefault(x => x.Id == dto.Id);
                if (h == null) return NotFound();
                h.UpdatedAt = DateTime.Now;
            }
            else
            {
                h = new Holiday();
                h.CreatedAt = DateTime.Now;
                h.CreatedBy = CurrentActor.Username(User) ?? "System";
                _context.Holidays.Add(h);
            }

            h.HolidayName = dto.HolidayName;
            h.FromDate = fromDate.Date;
            h.ToDate = toDate.Date;
            h.ScopeType = dto.ScopeType;
            h.ScopeValue = dto.ScopeType == "GLOBAL" ? "" : dto.ScopeValue;
            h.IsPaidHoliday = dto.IsPaidHoliday;
            h.Notes = dto.Notes;
            
            _context.SaveChanges();

            return Ok(new { success = true });
        }

        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public IActionResult ToggleStatus(int id, string newStatus)
        {
            var h = _context.Holidays.FirstOrDefault(x => x.Id == id);
            if (h == null) return NotFound();

            if (newStatus == "ACTIVE" || newStatus == "INACTIVE")
            {
                // If activating, check overlap again
                if (newStatus == "ACTIVE")
                {
                    var overlapping = _context.Holidays.FirstOrDefault(o => 
                        o.Id != id &&
                        o.Status == "ACTIVE" &&
                        o.ScopeType == h.ScopeType &&
                        o.ScopeValue == h.ScopeValue &&
                        ((h.FromDate >= o.FromDate && h.FromDate <= o.ToDate) || 
                         (h.ToDate >= o.FromDate && h.ToDate <= o.ToDate) ||
                         (h.FromDate <= o.FromDate && h.ToDate >= o.ToDate))
                    );

                    if (overlapping != null)
                    {
                        return BadRequest($"Không thể kích hoạt vì trùng thời gian với ngày lễ '{overlapping.HolidayName}'.");
                    }
                }

                h.Status = newStatus;
                h.UpdatedAt = DateTime.Now;
                _context.SaveChanges();
                return Ok(new { success = true });
            }
            return BadRequest();
        }
    }
}
