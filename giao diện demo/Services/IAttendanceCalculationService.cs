using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using giao_dien_demo.Models;

namespace giao_dien_demo.Services
{
    public interface IAttendanceCalculationService
    {
        IQueryable<Attendance> GetScopedAttendanceQuery(string role, int? currentEmployeeId, IReadOnlyCollection<string> leaderDepartments, string? filterDepartment = null);
        IQueryable<Attendance> GetScopedAttendanceQuery(string role, int? currentEmployeeId, string? leaderDepartment, string? filterDepartment = null);
        Task<WorkShift?> GetEffectiveWorkShiftAsync(string empCode, string empDept, DateTime date);
        Task ProcessCheckInTransactionAsync(Attendance attendance);
        Task ProcessAttendanceTransactionAsync(Attendance attendance);
    }
}
