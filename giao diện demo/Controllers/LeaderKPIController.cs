using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Services;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.LeaderOnly)]
    public class LeaderKPIController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILeadershipScopeService _scopeService;

        public LeaderKPIController(ApplicationDbContext context, ILeadershipScopeService scopeService)
        {
            _context = context;
            _scopeService = scopeService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? department, int? periodId, string? search, int page = 1, int pageSize = 25)
        {
            var allowedDepts = _scopeService.GetOverseenOrganizationalUnits();

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            if (!allowedDepts.Any())
            {
                var emptyVm = new LeaderKPIViewModel
                {
                    AllowedDepts = new List<string>(),
                    CurrentDept = string.Empty,
                    Periods = await _context.KPIPeriods.AsNoTracking().OrderByDescending(p => p.StartDate).ToListAsync(),
                    Search = search ?? string.Empty, Page = page, PageSize = pageSize
                };
                return View(emptyVm);
            }

            // Department filter validation: must be within leader's overseen units
            string activeDept = string.Empty;
            if (!string.IsNullOrEmpty(department) && !allowedDepts.Contains(department))
                return Forbid();
            if (!string.IsNullOrEmpty(department) && allowedDepts.Contains(department))
            {
                activeDept = department;
            }

            var targetDepts = !string.IsNullOrEmpty(activeDept) ? new List<string> { activeDept } : allowedDepts;

            // 1. Authoritative Completed KPI Evaluations
            var evalQuery = _context.KPIEvaluations
                .AsNoTracking()
                .Include(e => e.Employee)
                .Include(e => e.Period)
                .Where(e => e.Status == "COMPLETED" && e.Employee != null && e.Employee.Department != null && targetDepts.Contains(e.Employee.Department))
                .AsQueryable();

            if (periodId.HasValue && periodId.Value > 0)
            {
                evalQuery = evalQuery.Where(e => e.PeriodId == periodId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                evalQuery = evalQuery.Where(e => (e.Employee!.EmployeeCode != null && e.Employee.EmployeeCode.Contains(search)) || (e.Employee.Name != null && e.Employee.Name.Contains(search)));
            }

            int completedCount = await evalQuery.CountAsync();
            var scoredCount = await evalQuery.CountAsync(e => e.FinalScore.HasValue);
            decimal avgScore = scoredCount > 0
                ? Math.Round(await evalQuery.Where(e => e.FinalScore.HasValue).Select(e => e.FinalScore!.Value).AverageAsync(), 2)
                : 0;

            // Rating distribution
            var ratingDistribution = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var configuredRatings = await _context.KPIConfigs.AsNoTracking().OrderBy(c => c.KPIRating).Select(c => c.KPIRating).ToListAsync();
            foreach (var r in configuredRatings)
            {
                ratingDistribution[r] = 0;
            }

            var storedRatings = await evalQuery.Where(e => e.FinalRating != null).GroupBy(e => e.FinalRating!).Select(g => new { Rating = g.Key, Count = g.Count() }).ToListAsync();
            foreach (var rating in storedRatings) ratingDistribution[rating.Rating] = rating.Count;

            var completedEvals = await evalQuery
                .OrderByDescending(e => e.CompletedAt ?? e.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var evalRows = completedEvals.Select(e => new LeaderCompletedEvaluationRow
            {
                Id = e.Id,
                EmployeeId = e.EmployeeId,
                EmployeeName = e.Employee?.Name ?? "N/A",
                EmployeeCode = e.Employee?.EmployeeCode ?? "N/A",
                Department = e.EmployeeDepartmentSnapshot ?? e.Employee?.Department ?? "N/A",
                JobTitle = e.EmployeePositionSnapshot ?? e.Employee?.JobTitle ?? "N/A",
                PeriodName = e.Period?.Name ?? "N/A",
                FinalScore = e.FinalScore,
                FinalRating = e.FinalRating,
                CompletedAt = e.CompletedAt
            }).ToList();

            // 2. Current Snapshot from Employee.KPIScore (preserved for operational & payroll compatibility)
            var empQuery = _context.Employees
                .AsNoTracking()
                .Where(e => e.Department != null && targetDepts.Contains(e.Department) && e.Position != "Đã nghỉ việc");

            var currentEmployees = await empQuery.OrderByDescending(e => e.KPIScore).ToListAsync();

            var vm = new LeaderKPIViewModel
            {
                AllowedDepts = allowedDepts,
                CurrentDept = activeDept,
                Periods = await _context.KPIPeriods.AsNoTracking().OrderByDescending(p => p.StartDate).ToListAsync(),
                SelectedPeriodId = periodId,
                Search = search ?? string.Empty,
                Page = page,
                PageSize = pageSize,
                TotalEvaluations = completedCount,
                CompletedEvaluationsCount = completedCount,
                AverageFinalScore = avgScore,
                RatingDistribution = ratingDistribution,
                CompletedEvaluations = evalRows,
                CurrentEmployeeSnapshots = currentEmployees
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var allowedDepts = _scopeService.GetOverseenOrganizationalUnits();

            var eval = await _context.KPIEvaluations
                .AsNoTracking()
                .Include(e => e.Employee)
                .Include(e => e.Period)
                .Include(e => e.Targets)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (eval == null) return NotFound();

            // Scope check: current employee department must be in Leader's overseen units
            var currentDept = eval.Employee?.Department;
            if (string.IsNullOrEmpty(currentDept) || !_scopeService.CanAccessDepartment(currentDept))
            {
                return Forbid();
            }

            var targets = eval.Targets.OrderBy(t => t.Id).ToList();
            var vm = new LeaderKPIDetailViewModel
            {
                Evaluation = eval,
                Targets = targets,
                TotalWeight = targets.Sum(t => t.Weight)
            };

            return View(vm);
        }
    }
}
