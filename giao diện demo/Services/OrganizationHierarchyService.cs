using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using giao_dien_demo.Models;

namespace giao_dien_demo.Services
{
    public class OrganizationHierarchyService : IOrganizationHierarchyService
    {
        private readonly ApplicationDbContext _context;
        private readonly IManagerScopeService _managerScope;
        private readonly ILeadershipScopeService _leadershipScope;

        public OrganizationHierarchyService(
            ApplicationDbContext context,
            IManagerScopeService managerScope,
            ILeadershipScopeService leadershipScope)
        {
            _context = context;
            _managerScope = managerScope;
            _leadershipScope = leadershipScope;
        }

        public async Task<OrganizationHierarchyViewModel> BuildHierarchyAsync(ClaimsPrincipal userPrincipal, string? departmentFilter = null)
        {
            var result = new OrganizationHierarchyViewModel();
            var role = CurrentActor.Role(userPrincipal);
            result.CurrentUserRole = role;

            IQueryable<Employee> baseQuery;

            var currentUser = await CurrentActor.ResolveUserAsync(userPrincipal, _context);
            if (currentUser?.EmployeeId != null)
            {
                result.CurrentEmployeeId = currentUser.EmployeeId.Value;
            }

            bool isAdminOrHr = role == AppRoles.Admin || role == AppRoles.HR;

            if (isAdminOrHr)
            {
                baseQuery = _context.Employees.AsNoTracking().Where(e => e.Status != "Terminated");
                result.DataScopeDescription = "Toàn công ty (Quyền Quản trị / Nhân sự)";
            }
            else if (role == AppRoles.Leader)
            {
                baseQuery = _leadershipScope.GetScopedEmployeeQuery().AsNoTracking().Where(e => e.Status != "Terminated");
                var depts = _leadershipScope.GetOverseenOrganizationalUnits();
                result.DataScopeDescription = depts.Any() ? $"Phạm vi Lãnh đạo: {string.Join(", ", depts)}" : "Phạm vi Lãnh đạo";
            }
            else if (role == AppRoles.Manager)
            {
                baseQuery = _managerScope.GetScopedEmployeeQuery().AsNoTracking().Where(e => e.Status != "Terminated");
                var depts = _managerScope.GetManagedOrganizationalUnits();
                result.DataScopeDescription = depts.Any() ? $"Phạm vi Quản lý: {string.Join(", ", depts)}" : "Phạm vi Quản lý";
            }
            else // Employee
            {
                if (currentUser?.EmployeeId != null)
                {
                    var empId = currentUser.EmployeeId.Value;
                    var self = await _context.Employees.AsNoTracking()
                        .Include(e => e.Manager)
                        .FirstOrDefaultAsync(e => e.Id == empId);

                    if (self != null)
                    {
                        var allowedIds = new HashSet<int> { self.Id };
                        if (self.ManagerId.HasValue) allowedIds.Add(self.ManagerId.Value);

                        var directReports = await _context.Employees.AsNoTracking()
                            .Where(e => e.ManagerId == self.Id && e.Status != "Terminated")
                            .Select(e => e.Id)
                            .ToListAsync();
                        foreach (var drId in directReports) allowedIds.Add(drId);

                        baseQuery = _context.Employees.AsNoTracking()
                            .Where(e => allowedIds.Contains(e.Id));
                    }
                    else
                    {
                        baseQuery = _context.Employees.AsNoTracking().Where(e => false);
                    }
                }
                else
                {
                    baseQuery = _context.Employees.AsNoTracking().Where(e => false);
                }
                result.DataScopeDescription = "Tuyến báo cáo cá nhân";
            }

            // Available departments in scope
            result.AvailableDepartments = await baseQuery
                .Where(e => !string.IsNullOrEmpty(e.Department))
                .Select(e => e.Department!)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(departmentFilter))
            {
                result.SelectedDepartment = departmentFilter;
                baseQuery = baseQuery.Where(e => e.Department == departmentFilter);
            }

            var employees = await baseQuery
                .Include(e => e.Manager)
                .OrderBy(e => e.Department)
                .ThenBy(e => e.Name)
                .ToListAsync();

            result.TotalEmployees = employees.Count;

            // Load reporting history if Admin or HR
            var historyMap = new Dictionary<int, List<ReportingHistoryItemDto>>();
            if (isAdminOrHr && employees.Any())
            {
                var empIds = employees.Select(e => e.Id).ToList();
                var histories = await _context.EmployeeReportingHistories.AsNoTracking()
                    .Where(h => empIds.Contains(h.EmployeeId))
                    .Include(h => h.PreviousManager)
                    .Include(h => h.NewManager)
                    .Include(h => h.ChangedByUser)
                    .OrderByDescending(h => h.EffectiveDate)
                    .ThenByDescending(h => h.CreatedAt)
                    .ToListAsync();

                foreach (var h in histories)
                {
                    if (!historyMap.TryGetValue(h.EmployeeId, out var list))
                    {
                        list = new List<ReportingHistoryItemDto>();
                        historyMap[h.EmployeeId] = list;
                    }
                    list.Add(new ReportingHistoryItemDto
                    {
                        Id = h.Id,
                        PreviousManagerName = h.PreviousManager?.Name,
                        PreviousManagerCode = h.PreviousManager?.EmployeeCode,
                        NewManagerName = h.NewManager?.Name,
                        NewManagerCode = h.NewManager?.EmployeeCode,
                        ChangeReason = h.ChangeReason,
                        EffectiveDate = h.EffectiveDate,
                        ChangedByName = h.ChangedByUser?.FullName ?? h.ChangedByUser?.Username
                    });
                }
            }

            var nodeMap = new Dictionary<int, HierarchyNodeViewModel>();
            foreach (var emp in employees)
            {
                bool isSelf = result.CurrentEmployeeId.HasValue && emp.Id == result.CurrentEmployeeId.Value;
                bool canViewDetails = isAdminOrHr || isSelf;
                bool canManage = isAdminOrHr;

                historyMap.TryGetValue(emp.Id, out var empHistories);

                nodeMap[emp.Id] = new HierarchyNodeViewModel
                {
                    Id = emp.Id,
                    EmployeeCode = emp.EmployeeCode ?? $"NV{emp.Id:D3}",
                    Name = emp.Name ?? "Chưa đặt tên",
                    JobTitle = emp.JobTitle ?? emp.Position ?? "Nhân viên",
                    Department = emp.Department ?? "Chưa phân phòng",
                    Position = emp.Position ?? "Chính thức",
                    Status = emp.Status ?? "Active",
                    ManagerId = emp.ManagerId,
                    ManagerName = emp.Manager?.Name,
                    ManagerCode = emp.Manager?.EmployeeCode,
                    AvatarPath = emp.AvatarPath,
                    Email = emp.Email,
                    PhoneNumber = emp.PhoneNumber,
                    JoinDateFormatted = emp.BirthDate?.ToString("dd/MM/yyyy") ?? "Chưa cập nhật",
                    Level = 0,
                    IsCurrentUser = isSelf,
                    CanViewDetails = canViewDetails,
                    CanManageManager = canManage,
                    DirectReports = new List<HierarchyNodeViewModel>(),
                    ReportingHistories = empHistories ?? new List<ReportingHistoryItemDto>()
                };
            }

            var rootNodes = new List<HierarchyNodeViewModel>();
            var managersSet = new HashSet<int>();

            foreach (var node in nodeMap.Values)
            {
                if (!node.ManagerId.HasValue)
                {
                    rootNodes.Add(node);
                    continue;
                }

                if (nodeMap.TryGetValue(node.ManagerId.Value, out var parentNode))
                {
                    // Check cycle before adding
                    if (IsDescendantOf(parentNode, node.Id, nodeMap))
                    {
                        node.IsCycleDetected = true;
                        rootNodes.Add(node);
                    }
                    else
                    {
                        parentNode.DirectReports.Add(node);
                        managersSet.Add(parentNode.Id);
                    }
                }
                else
                {
                    // Manager exists in DB but outside current filtered scope
                    // Treat as root of current scoped tree, but mark as ManagerOutsideScope
                    node.IsManagerOutsideScope = true;
                    rootNodes.Add(node);
                }
            }

            // Calculate levels and descendant counts safely with visited guard
            var visited = new HashSet<int>();
            foreach (var root in rootNodes)
            {
                CalculateLevelAndCounts(root, 0, visited);
            }

            result.RootNodes = rootNodes.OrderByDescending(r => r.DirectReports.Count).ThenBy(r => r.Name).ToList();
            result.TotalManagers = managersSet.Count;
            result.ActualManagerNullCount = employees.Count(e => !e.ManagerId.HasValue);
            result.ManagerOutsideScopeCount = nodeMap.Values.Count(n => n.IsManagerOutsideScope);
            result.ConnectedRelationshipsCount = nodeMap.Values.Count(n => n.ManagerId.HasValue && !n.IsManagerOutsideScope);
            result.TotalUnassigned = result.StandaloneNodes.Count;

            return result;
        }

        private static bool IsDescendantOf(HierarchyNodeViewModel potentialParent, int targetNodeId, Dictionary<int, HierarchyNodeViewModel> map)
        {
            var current = potentialParent;
            var path = new HashSet<int>();

            while (current != null)
            {
                if (current.Id == targetNodeId) return true;
                if (!path.Add(current.Id)) break; // cycle in parent chain

                if (current.ManagerId.HasValue && map.TryGetValue(current.ManagerId.Value, out var nextParent))
                {
                    current = nextParent;
                }
                else
                {
                    break;
                }
            }

            return false;
        }

        private static int CalculateLevelAndCounts(HierarchyNodeViewModel node, int level, HashSet<int> visited)
        {
            if (!visited.Add(node.Id))
            {
                node.IsCycleDetected = true;
                return 0;
            }

            node.Level = level;
            int count = 0;

            foreach (var child in node.DirectReports)
            {
                count += 1 + CalculateLevelAndCounts(child, level + 1, visited);
            }

            node.TotalDescendantsCount = count;
            return count;
        }
    }
}
