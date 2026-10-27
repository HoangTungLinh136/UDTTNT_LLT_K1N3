using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using giao_dien_demo.Models;

namespace giao_dien_demo.Services
{
    public interface IManagerAssignmentService
    {
        Task<ManagerAssignmentDetailsViewModel> GetDetailsAsync(int employeeId);
        Task<(bool Success, string? Error, ManagerAssignment? Assignment)> CreateAsync(
            int employeeId, string organizationalUnit, DateTime effectiveFrom, DateTime? effectiveTo, string actor);
        Task<(bool Success, string? Error, ManagerAssignment? Assignment)> UpdateAsync(
            int assignmentId, string organizationalUnit, DateTime effectiveFrom, DateTime? effectiveTo, string actor);
        Task<(bool Success, string? Error)> EndAsync(int assignmentId, DateTime? effectiveTo, string actor);
    }
}
