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
    [Authorize(Policy = AppPolicies.ManagerOnly)]
    [AutoValidateAntiforgeryToken]
    public class ManagerKPIController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IKPIManagementService _kpiService;
        private readonly IManagerScopeService _scopeService;

        public ManagerKPIController(
            ApplicationDbContext context,
            IKPIManagementService kpiService,
            IManagerScopeService scopeService)
        {
            _context = context;
            _kpiService = kpiService;
            _scopeService = scopeService;
        }

        private async Task<User?> GetCurrentUserAsync()
        {
            return await CurrentActor.ResolveUserAsync(User, _context);
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? periodId, string status = "", string search = "", int page = 1)
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null) return Forbid();

            // 1. Get scoped employee query (authoritative active assignments)
            var scopedEmployeesQuery = _scopeService.GetScopedEmployeeQuery().AsNoTracking();
            var scopedEmployeeIds = await scopedEmployeesQuery.Select(e => e.Id).ToListAsync();

            // Handle manager with 0 assignments
            if (!scopedEmployeeIds.Any())
            {
                var emptyVm = new ManagerKPIListViewModel
                {
                    Items = new List<ManagerKPIEvaluationRow>(),
                    AvailablePeriods = await _context.KPIPeriods.AsNoTracking().OrderByDescending(p => p.StartDate).ToListAsync(),
                    TotalScopedEmployees = 0,
                    Page = 1,
                    TotalItems = 0,
                    TotalEvaluations = 0,
                    DraftEvaluations = 0,
                    EvaluatingEvaluations = 0,
                    CompletedEvaluations = 0
                };
                return View(emptyVm);
            }

            // 2. Query evaluations scoped to this manager's employees and owned by this manager
            var scopedEvaluationQuery = _context.KPIEvaluations
                .AsNoTracking()
                .Where(e => scopedEmployeeIds.Contains(e.EmployeeId) && e.ManagerUserId == currentUser.Id);
            var query = _context.KPIEvaluations
                .AsNoTracking()
                .Include(e => e.Employee)
                .Include(e => e.Period)
                .Include(e => e.Targets)
                .Where(e => scopedEmployeeIds.Contains(e.EmployeeId) && e.ManagerUserId == currentUser.Id)
                .AsQueryable();

            if (periodId.HasValue && periodId.Value > 0)
            {
                query = query.Where(e => e.PeriodId == periodId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(e => e.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(e => (e.Employee != null && (e.Employee.Name.Contains(search) || (e.Employee.EmployeeCode != null && e.Employee.EmployeeCode.Contains(search)))));
            }

            int pageSize = 10;
            int total = await query.CountAsync();
            var evaluations = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var rowList = evaluations.Select(e => new ManagerKPIEvaluationRow
            {
                Id = e.Id,
                PeriodId = e.PeriodId,
                PeriodName = e.Period?.Name ?? "N/A",
                EmployeeId = e.EmployeeId,
                EmployeeName = e.Employee?.Name ?? "N/A",
                EmployeeCode = e.Employee?.EmployeeCode ?? "N/A",
                Department = e.EmployeeDepartmentSnapshot ?? e.Employee?.Department ?? "N/A",
                Position = e.EmployeePositionSnapshot ?? e.Employee?.JobTitle ?? "N/A",
                Status = e.Status,
                TargetCount = e.Targets.Count,
                ProgressAverage = e.Targets.Any() ? Math.Round(e.Targets.Average(t => t.Progress), 1) : 0,
                FinalScore = e.FinalScore,
                FinalRating = e.FinalRating,
                CreatedAt = e.CreatedAt,
                IsActionable = scopedEmployeeIds.Contains(e.EmployeeId)
            }).ToList();

            var vm = new ManagerKPIListViewModel
            {
                Items = rowList,
                AvailablePeriods = await _context.KPIPeriods.AsNoTracking().OrderByDescending(p => p.StartDate).ToListAsync(),
                SelectedPeriodId = periodId,
                StatusFilter = status,
                SearchTerm = search,
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                TotalScopedEmployees = scopedEmployeeIds.Count,
                TotalEvaluations = await scopedEvaluationQuery.CountAsync(),
                DraftEvaluations = await scopedEvaluationQuery.CountAsync(e => e.Status == "DRAFT"),
                EvaluatingEvaluations = await scopedEvaluationQuery.CountAsync(e => e.Status == "EVALUATING"),
                CompletedEvaluations = await scopedEvaluationQuery.CountAsync(e => e.Status == "COMPLETED")
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? periodId)
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null) return Forbid();

            var periods = await _context.KPIPeriods
                .AsNoTracking()
                .Where(p => p.Status == "PLANNED" || p.Status == "ACTIVE")
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();

            var employees = await _scopeService.GetScopedEmployeeQuery()
                .AsNoTracking()
                .Where(e => e.Position != "Đã nghỉ việc" && e.Status != "Offboarded" && e.Status != "Terminated" && e.Status != "Suspended")
                .OrderBy(e => e.Name)
                .ToListAsync();

            var vm = new CreateEvaluationViewModel
            {
                Periods = periods,
                ScopedEmployees = employees,
                SelectedPeriodId = periodId
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateEvaluationInput input)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Dữ liệu nhập không hợp lệ.";
                return RedirectToAction(nameof(Create), new { periodId = input.PeriodId });
            }

            try
            {
                var evaluation = await _kpiService.CreateEvaluationAsync(input.PeriodId, input.EmployeeId);
                TempData["SuccessMessage"] = "Khởi tạo đánh giá KPI thành công (Bản nháp). Hãy thiết lập các mục tiêu.";
                return RedirectToAction(nameof(Details), new { id = evaluation.Id });
            }
            catch (UnauthorizedAccessException ex)
            {
                TempData["ErrorMessage"] = "Nhân viên không nằm trong phạm vi quản lý của bạn hoặc bạn không có quyền.";
                return RedirectToAction(nameof(Create), new { periodId = input.PeriodId });
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = "Nhân viên đã có đánh giá KPI trong kỳ này.";
                }
                else
                {
                    TempData["ErrorMessage"] = ex.Message;
                }
                return RedirectToAction(nameof(Create), new { periodId = input.PeriodId });
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Đã xảy ra lỗi không mong muốn khi tạo đánh giá KPI.";
                return RedirectToAction(nameof(Create), new { periodId = input.PeriodId });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null) return Forbid();

            var eval = await _context.KPIEvaluations
                .Include(e => e.Period)
                .Include(e => e.Employee)
                .Include(e => e.Targets)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (eval == null) return NotFound();

            // Ownership check
            if (eval.ManagerUserId != currentUser.Id) return Forbid();

            // Scope check: employee in active manager scope?
            bool inScope = await _scopeService.GetScopedEmployeeQuery().AnyAsync(e => e.Id == eval.EmployeeId);
            if (!inScope)
            {
                // A previously owned evaluation must not become a direct-URL
                // read surface after the employee leaves the manager's active scope.
                return Forbid();
            }

            var targets = eval.Targets.OrderBy(t => t.Id).ToList();
            decimal totalWeight = targets.Sum(t => t.Weight);
            decimal progressAvg = targets.Any() ? Math.Round(targets.Average(t => t.Progress), 1) : 0;
            bool isAllScored = targets.Any() && targets.All(t => t.ManagerScore != null);
            decimal previewScore = isAllScored ? Math.Round(targets.Sum(t => (t.ManagerScore ?? 0) * t.Weight / 100m), 2) : 0;

            var availableRatings = await _context.KPIConfigs.AsNoTracking().OrderBy(c => c.KPIRating).ToListAsync();

            var vm = new ManagerEvaluationDetailViewModel
            {
                Evaluation = eval,
                Targets = targets,
                TotalWeight = totalWeight,
                ProgressAverage = progressAvg,
                PreviewFinalScore = previewScore,
                IsAllScored = isAllScored,
                AvailableRatings = availableRatings,
                InScope = inScope,
                IsActionable = inScope
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> AddTarget(int evaluationId, AddTargetInput input)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(Details), new { id = evaluationId });
            }

            try
            {
                var target = new KPITarget
                {
                    Title = input.Title.Trim(),
                    Description = input.Description?.Trim(),
                    Weight = input.Weight
                };

                await _kpiService.AddTargetAsync(evaluationId, target);
                TempData["SuccessMessage"] = "Đã thêm mục tiêu thành công.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id = evaluationId });
        }

        [HttpPost]
        public async Task<IActionResult> EditTarget(int evaluationId, EditTargetInput input)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(Details), new { id = evaluationId });
            }

            try
            {
                await _kpiService.UpdateTargetAsync(input.TargetId, input.Title.Trim(), input.Description?.Trim(), input.Weight);
                TempData["SuccessMessage"] = "Đã cập nhật mục tiêu thành công.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id = evaluationId });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveTarget(int evaluationId, int targetId)
        {
            try
            {
                await _kpiService.RemoveTargetAsync(targetId);
                TempData["SuccessMessage"] = "Đã xóa mục tiêu thành công.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id = evaluationId });
        }

        [HttpPost]
        public async Task<IActionResult> Activate(int id)
        {
            try
            {
                await _kpiService.ActivateEvaluationAsync(id);
                TempData["SuccessMessage"] = "Đã kích hoạt đánh giá KPI thành công. Nhân viên bắt đầu có thể cập nhật tiến độ.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (UnauthorizedAccessException)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền kích hoạt đánh giá này.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi kích hoạt đánh giá KPI.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> StartEvaluation(int id)
        {
            try
            {
                await _kpiService.StartEvaluatingAsync(id);
                TempData["SuccessMessage"] = "Đã chuyển trạng thái sang Đang đánh giá. Quản lý có thể chấm điểm các mục tiêu.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> SaveScores(int evaluationId, List<TargetScoreInput> scores)
        {
            if (scores == null || !scores.Any())
            {
                TempData["ErrorMessage"] = "Không có điểm nào được gửi.";
                return RedirectToAction(nameof(Details), new { id = evaluationId });
            }

            try
            {
                foreach (var item in scores)
                {
                    if (item.Score < 0 || item.Score > 100)
                    {
                        TempData["ErrorMessage"] = "Điểm phải nằm trong thang điểm 0 - 100.";
                        return RedirectToAction(nameof(Details), new { id = evaluationId });
                    }
                    await _kpiService.UpdateManagerScoreAsync(item.TargetId, item.Score);
                }

                TempData["SuccessMessage"] = "Đã lưu điểm đánh giá thành công.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id = evaluationId });
        }

        [HttpPost]
        public async Task<IActionResult> Finalize(int id, FinalizeEvaluationInput input)
        {
            if (string.IsNullOrWhiteSpace(input.FinalRating))
            {
                TempData["ErrorMessage"] = "Vui lòng chọn xếp loại cuối cùng.";
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                await _kpiService.FinalizeEvaluationAsync(id, input.FinalRating.Trim());
                TempData["SuccessMessage"] = "Đã hoàn thành đánh giá KPI thành công. Dữ liệu đã được khóa và xếp loại đã được cập nhật cho nhân sự.";
            }
            catch (ArgumentException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi hoàn tất đánh giá KPI.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                await _kpiService.CancelEvaluationAsync(id);
                TempData["SuccessMessage"] = "Đã hủy đánh giá KPI thành công.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
