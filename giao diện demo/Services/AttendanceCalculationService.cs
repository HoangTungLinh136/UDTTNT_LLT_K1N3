using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;

namespace giao_dien_demo.Services
{
    public class AttendanceCalculationService : IAttendanceCalculationService
    {
        private readonly ApplicationDbContext _context;

        public AttendanceCalculationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public IQueryable<Attendance> GetScopedAttendanceQuery(
            string role,
            int? currentEmployeeId,
            IReadOnlyCollection<string> leaderDepartments,
            string? filterDepartment = null)
        {
            var query = _context.Attendances.AsQueryable();

            if (role == "Employee")
            {
                if (currentEmployeeId.HasValue)
                {
                    query = query.Where(a => a.EmployeeId == currentEmployeeId.Value);
                }
                else
                {
                    // Fallback for unlinked employee
                    query = query.Where(a => false);
                }
            }
            else if (role == "Leader")
            {
                // Leader scope is an authorization input, not an optional filter.
                // An empty or missing active assignment must fail closed before the
                // query is materialized. The collection intentionally supports a
                // leader overseeing multiple organizational units.
                var allowedDepartments = (leaderDepartments ?? Array.Empty<string>())
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .Distinct()
                    .ToArray();

                if (allowedDepartments.Length == 0)
                {
                    return query.Where(_ => false);
                }

                query = query.Where(a => a.Employee != null && allowedDepartments.Contains(a.Employee.Department));

                // A requested department may narrow an authorized scope, never
                // broaden it. Invalid departments therefore return an empty query.
                if (!string.IsNullOrWhiteSpace(filterDepartment))
                {
                    if (!allowedDepartments.Contains(filterDepartment))
                    {
                        return query.Where(_ => false);
                    }

                    query = query.Where(a => a.Employee != null && a.Employee.Department == filterDepartment);
                }

                return query;
            }
            // HR and Admin can see everything

            if (!string.IsNullOrEmpty(filterDepartment))
            {
                query = query.Where(a => a.Employee != null && a.Employee.Department == filterDepartment);
            }

            return query;
        }

        public IQueryable<Attendance> GetScopedAttendanceQuery(string role, int? currentEmployeeId, string? leaderDepartment, string? filterDepartment = null)
        {
            var query = _context.Attendances.AsQueryable();

            if (role == "Employee")
            {
                if (currentEmployeeId.HasValue)
                {
                    query = query.Where(a => a.EmployeeId == currentEmployeeId.Value);
                }
                else
                {
                    // Fallback for unlinked employee
                    query = query.Where(a => false);
                }
            }
            else if (role == "Leader" && !string.IsNullOrEmpty(leaderDepartment))
            {
                query = query.Where(a => a.Employee != null && a.Employee.Department == leaderDepartment);
            }
            // HR and Admin can see everything

            if (!string.IsNullOrEmpty(filterDepartment))
            {
                query = query.Where(a => a.Employee != null && a.Employee.Department == filterDepartment);
            }

            return query;
        }

        public async Task<WorkShift?> GetEffectiveWorkShiftAsync(string empCode, string empDept, DateTime date)
        {
            var assignments = await _context.WorkShiftAssignments
                .Include(a => a.WorkShift)
                .Where(a => (!a.EffectiveFrom.HasValue || a.EffectiveFrom.Value.Date <= date) &&
                            (!a.EffectiveTo.HasValue || a.EffectiveTo.Value.Date >= date))
                .ToListAsync();

            var assignment = assignments.FirstOrDefault(a => a.TargetType == "Employee" && a.TargetValue == empCode)
                          ?? assignments.FirstOrDefault(a => a.TargetType == "Department" && a.TargetValue == empDept)
                          ?? assignments.FirstOrDefault(a => a.TargetType == "Company");

            if (assignment?.WorkShift != null)
            {
                return assignment.WorkShift;
            }

            return await _context.WorkShifts.FirstOrDefaultAsync(s => s.Status == "Active");
        }

        public async Task ProcessCheckInTransactionAsync(Attendance attendance)
        {
            var date = attendance.CheckIn.Date;

            // 1. Get Employee and Department
            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.Id == attendance.EmployeeId);
            string empCode = emp?.EmployeeCode ?? "";
            string empDept = emp?.Department ?? "";

            // 2. Check for Holidays
            bool isHoliday = await _context.Holidays.AnyAsync(h => 
                h.Status == "ACTIVE" && h.IsPaidHoliday &&
                h.FromDate.Date <= date && h.ToDate.Date >= date &&
                (h.ScopeType == "GLOBAL" || 
                (h.ScopeType == "DEPARTMENT" && h.ScopeValue == empDept) ||
                (h.ScopeType == "EMPLOYEE" && h.ScopeValue == empCode))
            );

            // 3. Check for Approved Leaves
            bool isLeave = await _context.Leaves.AnyAsync(l => 
                l.Status == "Đã duyệt" &&
                l.FromDate.Date <= date && l.ToDate.Date >= date &&
                l.EmployeeId == attendance.EmployeeId
            );

            // 4. Special Case: Holiday, Leave, or Sunday
            if (isHoliday || isLeave || date.DayOfWeek == DayOfWeek.Sunday)
            {
                attendance.LateMinutes = 0;
                attendance.StatusNote = isHoliday ? "Làm việc ngày Lễ" : (isLeave ? "Làm việc ngày Phép" : "Làm việc Chủ Nhật");
                return;
            }

            // 5. Determine Active WorkShift
            var shift = await GetEffectiveWorkShiftAsync(empCode, empDept, date);

            // 6. Check Active Attendance Rule
            var rule = await _context.AttendanceRuleVersions
                .Where(r => r.Status == "Active" && r.EffectiveFrom.Date <= date && (!r.EffectiveTo.HasValue || r.EffectiveTo.Value.Date >= date))
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync();

            int lateGrace = shift?.LateGraceMinutes ?? rule?.LateGraceMinutes ?? 0;

            // 7. Calculate Late based on effective shift or fallback
            if (shift != null)
            {
                TimeSpan lateThreshold = shift.StartTime.Add(TimeSpan.FromMinutes(lateGrace));
                if (attendance.CheckIn.TimeOfDay > lateThreshold)
                {
                    attendance.LateMinutes = (int)(attendance.CheckIn.TimeOfDay - shift.StartTime).TotalMinutes;
                    attendance.StatusNote = $"Đi muộn ({attendance.LateMinutes}p)";
                }
                else
                {
                    attendance.LateMinutes = 0;
                    attendance.StatusNote = "Đúng giờ";
                }
            }
            else
            {
                // Fallback standard morning/afternoon if no shift exists
                var morningStart = new TimeSpan(7, 30, 0).Add(TimeSpan.FromMinutes(lateGrace));
                var morningEnd = new TimeSpan(11, 30, 0);
                var afternoonStart = new TimeSpan(13, 0, 0).Add(TimeSpan.FromMinutes(lateGrace));
                var timeIn = attendance.CheckIn.TimeOfDay;

                if (timeIn > morningStart && timeIn <= morningEnd)
                {
                    attendance.LateMinutes = (int)(timeIn - new TimeSpan(7, 30, 0)).TotalMinutes;
                    attendance.StatusNote = $"Đi muộn sáng ({attendance.LateMinutes}p)";
                }
                else if (timeIn > afternoonStart && timeIn <= new TimeSpan(17, 0, 0))
                {
                    attendance.LateMinutes = (int)(timeIn - new TimeSpan(13, 0, 0)).TotalMinutes;
                    attendance.StatusNote = $"Đi muộn chiều ({attendance.LateMinutes}p)";
                }
                else
                {
                    attendance.LateMinutes = 0;
                    attendance.StatusNote = "Đúng giờ";
                }
            }
        }

        public async Task ProcessAttendanceTransactionAsync(Attendance attendance)
        {
            if (attendance.CheckOut == null) return;
            
            var date = attendance.CheckIn.Date;
            
            // 1. Get Employee and Department
            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.Id == attendance.EmployeeId);
            string empCode = emp?.EmployeeCode ?? "";
            string empDept = emp?.Department ?? "";

            // 2. Check for Holidays
            bool isHoliday = await _context.Holidays.AnyAsync(h => 
                h.Status == "ACTIVE" && h.IsPaidHoliday &&
                h.FromDate.Date <= date && h.ToDate.Date >= date &&
                (h.ScopeType == "GLOBAL" || 
                (h.ScopeType == "DEPARTMENT" && h.ScopeValue == empDept) ||
                (h.ScopeType == "EMPLOYEE" && h.ScopeValue == empCode))
            );

            // 3. Check for Approved Leaves
            bool isLeave = await _context.Leaves.AnyAsync(l => 
                l.Status == "Đã duyệt" &&
                l.FromDate.Date <= date && l.ToDate.Date >= date &&
                l.EmployeeId == attendance.EmployeeId
            );

            // 4. Determine Active WorkShift
            WorkShift? shift = await GetEffectiveWorkShiftAsync(empCode, empDept, date);

            // 5. Check Active Attendance Rule
            var rule = await _context.AttendanceRuleVersions
                .Where(r => r.Status == "Active" && r.EffectiveFrom.Date <= date && (!r.EffectiveTo.HasValue || r.EffectiveTo.Value.Date >= date))
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync();

            double dailyOtThreshold = rule?.DailyOtThreshold ?? 8.0;
            int lateGrace = shift?.LateGraceMinutes ?? rule?.LateGraceMinutes ?? 0;
            
            TimeSpan duration = attendance.CheckOut.Value - attendance.CheckIn;
            double actualHours = duration.TotalHours;

            // 6. Special Case: Holiday or Leave -> Entire time is OT (or extra pay)
            if (isHoliday || isLeave || date.DayOfWeek == DayOfWeek.Sunday)
            {
                attendance.StandardHours = 0;
                
                // Subtract break time if Shift provides it
                if (shift != null && actualHours * 60 > shift.BreakDurationMinutes)
                {
                    actualHours -= shift.BreakDurationMinutes / 60.0;
                }
                
                attendance.OvertimeHours = Math.Max(0, actualHours);
                attendance.LateMinutes = 0;
                attendance.StatusNote = isHoliday ? "Làm thêm ngày Lễ" : (isLeave ? "Làm thêm ngày Phép" : "Làm thêm Chủ Nhật");
                return;
            }

            // 7. Normal Shift Calculation
            if (shift == null)
            {
                // Fallback if no shift is defined
                attendance.StandardHours = Math.Min(actualHours, dailyOtThreshold);
                attendance.OvertimeHours = Math.Max(0, actualHours - dailyOtThreshold);
                attendance.LateMinutes = 0;
                attendance.StatusNote = "Không có lịch làm việc (Tự động tính giờ)";
                return;
            }

            // Deduct break duration
            if (actualHours * 60 > shift.BreakDurationMinutes)
            {
                actualHours -= shift.BreakDurationMinutes / 60.0;
            }

            attendance.StandardHours = Math.Min(actualHours, dailyOtThreshold);
            attendance.OvertimeHours = Math.Max(0, actualHours - dailyOtThreshold);

            // 8. Calculate Late/Early (Time-based comparison)
            TimeSpan lateThreshold = shift.StartTime.Add(TimeSpan.FromMinutes(lateGrace));
            
            if (attendance.CheckIn.TimeOfDay > lateThreshold)
            {
                attendance.LateMinutes = (int)(attendance.CheckIn.TimeOfDay - shift.StartTime).TotalMinutes;
                attendance.StatusNote = $"Đi muộn ({attendance.LateMinutes}p)";
            }
            else
            {
                attendance.LateMinutes = 0;
                attendance.StatusNote = "Đúng giờ";
            }
        }
    }
}
