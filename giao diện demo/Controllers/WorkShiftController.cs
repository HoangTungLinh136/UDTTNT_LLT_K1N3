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
    public class WorkShiftController : Controller
    {
        private readonly ApplicationDbContext _context;

        public WorkShiftController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Authorize(Policy = AppPolicies.HROnly)]
        public IActionResult Index(string keyword = "", string type = "", string status = "")
        {

            var query = _context.WorkShifts.AsQueryable();
            var allShifts = _context.WorkShifts.ToList();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(s => s.ShiftCode.Contains(keyword) || s.ShiftName.Contains(keyword));
            }
            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(s => s.ShiftType == type);
            }
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(s => s.Status == status);
            }

            var shifts = query.OrderByDescending(s => s.CreatedAt).ToList();

            var vm = new WorkShiftViewModel
            {
                Shifts = shifts,
                TotalShifts = allShifts.Count,
                ActiveShifts = allShifts.Count(s => s.Status == "Active"),
                InactiveShifts = allShifts.Count(s => s.Status == "Inactive"),
                OvernightShifts = allShifts.Count(s => s.IsOvernight),
                SearchKeyword = keyword,
                FilterType = type,
                FilterStatus = status
            };

            // Master data for types (mocked for now, can be from DB)
            var types = new List<string> { "Hành chính", "Làm ca", "Bán thời gian" };
            vm.ShiftTypes = types.Select(t => new SelectListItem { Value = t, Text = t }).ToList();

            // Populate assignment options
            var depts = _context.Employees.Where(e => !string.IsNullOrEmpty(e.Department)).Select(e => e.Department!).Distinct().ToList();
            vm.Departments = depts.Select(d => new SelectListItem { Value = d, Text = d }).ToList();

            var emps = _context.Employees.Select(e => new { e.EmployeeCode, e.Name }).ToList();
            vm.Employees = emps.Select(e => new SelectListItem { Value = e.EmployeeCode, Text = $"{e.EmployeeCode} - {e.Name}" }).ToList();

            return View(vm);
        }

        [HttpGet]
        [Authorize(Roles = $"{AppRoles.HR},{AppRoles.Leader},{AppRoles.Manager}")]
        public IActionResult GetShift(int id)
        {
            var shift = _context.WorkShifts.FirstOrDefault(s => s.Id == id);
            if (shift == null) return NotFound();

            var assignments = _context.WorkShiftAssignments.Where(a => a.WorkShiftId == id).ToList();

            var dto = new WorkShiftDto
            {
                Id = shift.Id,
                ShiftCode = shift.ShiftCode,
                ShiftName = shift.ShiftName,
                ShiftType = shift.ShiftType,
                StartTime = shift.StartTime.ToString(@"hh\:mm"),
                EndTime = shift.EndTime.ToString(@"hh\:mm"),
                BreakDurationMinutes = shift.BreakDurationMinutes,
                LateGraceMinutes = shift.LateGraceMinutes,
                EarlyLeaveGraceMinutes = shift.EarlyLeaveGraceMinutes,
                IsOvernight = shift.IsOvernight,
                IsOvertimeEnabled = shift.IsOvertimeEnabled,
                Status = shift.Status,
                Notes = shift.Notes,
                AssignedDepartments = assignments.Where(a => a.TargetType == "Department").Select(a => a.TargetValue).ToList(),
                AssignedEmployees = assignments.Where(a => a.TargetType == "Employee").Select(a => a.TargetValue).ToList()
            };

            return Json(dto);
        }

        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public IActionResult SaveShift([FromBody] WorkShiftDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ShiftCode) || string.IsNullOrWhiteSpace(dto.ShiftName))
            {
                return BadRequest("Mã ca và tên ca không được để trống.");
            }

            if (dto.LateGraceMinutes < 0 || dto.EarlyLeaveGraceMinutes < 0 || dto.BreakDurationMinutes < 0)
            {
                return BadRequest("Thời gian (Break, Grace) không hợp lệ (không được nhỏ hơn 0).");
            }

            TimeSpan startTime, endTime;
            if (!TimeSpan.TryParse(dto.StartTime, out startTime) || !TimeSpan.TryParse(dto.EndTime, out endTime))
            {
                return BadRequest("Giờ bắt đầu / kết thúc không hợp lệ.");
            }

            // Check duplicate Code
            var existingWithCode = _context.WorkShifts.FirstOrDefault(s => s.ShiftCode == dto.ShiftCode && s.Id != dto.Id);
            if (existingWithCode != null)
            {
                return BadRequest("Mã ca đã tồn tại trong hệ thống.");
            }

            WorkShift shift;
            bool isNew = false;
            if (dto.Id > 0)
            {
                shift = _context.WorkShifts.FirstOrDefault(s => s.Id == dto.Id);
                if (shift == null) return NotFound();
                shift.UpdatedAt = DateTime.Now;
            }
            else
            {
                shift = new WorkShift();
                shift.CreatedAt = DateTime.Now;
                shift.CreatedBy = CurrentActor.Username(User) ?? "System";
                _context.WorkShifts.Add(shift);
                isNew = true;
            }

            shift.ShiftCode = dto.ShiftCode;
            shift.ShiftName = dto.ShiftName;
            shift.ShiftType = dto.ShiftType;
            shift.StartTime = startTime;
            shift.EndTime = endTime;
            shift.BreakDurationMinutes = dto.BreakDurationMinutes;
            shift.LateGraceMinutes = dto.LateGraceMinutes;
            shift.EarlyLeaveGraceMinutes = dto.EarlyLeaveGraceMinutes;
            shift.IsOvernight = dto.IsOvernight;
            shift.IsOvertimeEnabled = dto.IsOvertimeEnabled;
            shift.Status = dto.Status;
            shift.Notes = dto.Notes;

            _context.SaveChanges();

            // Update Assignments
            var currentAssignments = _context.WorkShiftAssignments.Where(a => a.WorkShiftId == shift.Id).ToList();
            _context.WorkShiftAssignments.RemoveRange(currentAssignments);

            foreach (var dept in dto.AssignedDepartments)
            {
                if (!string.IsNullOrWhiteSpace(dept))
                {
                    _context.WorkShiftAssignments.Add(new WorkShiftAssignment
                    {
                        WorkShiftId = shift.Id,
                        TargetType = "Department",
                        TargetValue = dept
                    });
                }
            }

            foreach (var emp in dto.AssignedEmployees)
            {
                if (!string.IsNullOrWhiteSpace(emp))
                {
                    _context.WorkShiftAssignments.Add(new WorkShiftAssignment
                    {
                        WorkShiftId = shift.Id,
                        TargetType = "Employee",
                        TargetValue = emp
                    });
                }
            }

            _context.SaveChanges();

            return Ok(new { success = true, id = shift.Id });
        }

        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public IActionResult ToggleStatus(int id, string newStatus)
        {
            var shift = _context.WorkShifts.FirstOrDefault(s => s.Id == id);
            if (shift == null) return NotFound();

            if (newStatus == "Active" || newStatus == "Inactive")
            {
                shift.Status = newStatus;
                shift.UpdatedAt = DateTime.Now;
                _context.SaveChanges();
                return Ok(new { success = true });
            }
            
            return BadRequest();
        }
    }
}
