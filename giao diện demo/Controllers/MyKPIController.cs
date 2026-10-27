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
    [Authorize(Policy = AppPolicies.EmployeeOnly)]
    [AutoValidateAntiforgeryToken]
    public class MyKPIController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IKPIManagementService _kpiService;

        public MyKPIController(ApplicationDbContext context, IKPIManagementService kpiService)
        {
            _context = context;
            _kpiService = kpiService;
        }

        private async Task<(User? User, Employee? Employee)> GetCurrentActorAsync()
        {
            var user = await CurrentActor.ResolveUserAsync(User, _context);
            if (user == null || !user.EmployeeId.HasValue) return (user, null);

            var employee = await _context.Employees.FindAsync(user.EmployeeId.Value);
            return (user, employee);
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? periodId, int page = 1)
        {
            var (user, employee) = await GetCurrentActorAsync();
            if (employee == null)
            {
                ViewBag.ErrorMessage = "Tài khoản của bạn chưa được liên kết với hồ sơ nhân viên nào.";
                return View(new MyKPIListViewModel());
            }

            // Exclude DRAFT and cancelled-from-draft evaluations from employee view
            var previouslyActivatedEvalIds = _context.AuditLogs
                .Where(a => a.Module == "KPI" && a.Action == "Activate")
                .Select(a => a.ResourceId);

            var query = _context.KPIEvaluations
                .AsNoTracking()
                .Include(e => e.Period)
                .Include(e => e.Targets)
                .Where(e => e.EmployeeId == employee.Id && 
                            (e.Status == "ACTIVE" || e.Status == "EVALUATING" || e.Status == "COMPLETED" ||
                             (e.Status == "CANCELLED" && previouslyActivatedEvalIds.Contains(e.Id.ToString()))))
                .AsQueryable();

            if (periodId.HasValue && periodId.Value > 0)
            {
                query = query.Where(e => e.PeriodId == periodId.Value);
            }

            int pageSize = 10;
            int total = await query.CountAsync();
            var evaluations = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var rowList = evaluations.Select(e => new MyKPIEvaluationRow
            {
                Id = e.Id,
                PeriodName = e.Period?.Name ?? "N/A",
                StartDate = e.Period?.StartDate ?? DateTime.MinValue,
                EndDate = e.Period?.EndDate ?? DateTime.MinValue,
                Status = e.Status,
                TargetCount = e.Targets.Count,
                ProgressAverage = e.Targets.Any() ? Math.Round(e.Targets.Average(t => t.Progress), 1) : 0,
                FinalScore = e.FinalScore,
                FinalRating = e.FinalRating,
                CompletedAt = e.CompletedAt
            }).ToList();

            var availablePeriods = await _context.KPIPeriods
                .AsNoTracking()
                .Where(p => p.Status == "ACTIVE" || p.Status == "CLOSED")
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();

            var vm = new MyKPIListViewModel
            {
                Items = rowList,
                AvailablePeriods = availablePeriods,
                SelectedPeriodId = periodId,
                Page = page,
                PageSize = pageSize,
                TotalItems = total
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var (user, employee) = await GetCurrentActorAsync();
            if (employee == null) return Forbid();

            var eval = await _context.KPIEvaluations
                .Include(e => e.Period)
                .Include(e => e.Targets)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (eval == null) return NotFound();

            // Strict IDOR protection: only own evaluations
            if (eval.EmployeeId != employee.Id) return Forbid();

            // Never expose DRAFT to employee
            if (eval.Status == "DRAFT") return NotFound();

            // If CANCELLED, ensure it was previously activated before displaying history
            if (eval.Status == "CANCELLED")
            {
                bool wasActivated = await _context.AuditLogs.AnyAsync(a => a.Module == "KPI" && a.Action == "Activate" && a.ResourceId == eval.Id.ToString());
                if (!wasActivated) return NotFound();
            }

            var targets = eval.Targets.OrderBy(t => t.Id).ToList();
            var vm = new MyKPIDetailViewModel
            {
                Evaluation = eval,
                Targets = targets,
                TotalWeight = targets.Sum(t => t.Weight),
                ProgressAverage = targets.Any() ? Math.Round(targets.Average(t => t.Progress), 1) : 0
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProgress([FromBody] UpdateProgressInput input)
        {
            if (input == null || !ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Dữ liệu nhập không hợp lệ." });
            }

            if (input.Progress < 0 || input.Progress > 100)
            {
                return BadRequest(new { success = false, message = "Tiến độ phải nằm trong khoảng từ 0% đến 100%." });
            }

            try
            {
                // Authoritative service method checks ownership, status == ACTIVE, and updates progress
                await _kpiService.UpdateEmployeeProgressAsync(input.TargetId, input.Progress);
                return Ok(new { success = true, message = "Cập nhật tiến độ thành công." });
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(403, new { success = false, message = "Bạn không có quyền cập nhật mục tiêu này." });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, message = "Không tìm thấy mục tiêu." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi cập nhật tiến độ." });
            }
        }
    }
}
