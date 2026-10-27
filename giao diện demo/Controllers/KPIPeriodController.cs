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
    [Authorize(Policy = AppPolicies.HROnly)]
    [AutoValidateAntiforgeryToken]
    public class KPIPeriodController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IKPIManagementService _kpiService;

        public KPIPeriodController(ApplicationDbContext context, IKPIManagementService kpiService)
        {
            _context = context;
            _kpiService = kpiService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string status = "", string search = "", int page = 1)
        {
            var query = _context.KPIPeriods.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(p => p.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p => p.Name.Contains(search));
            }

            int pageSize = 10;
            int total = await query.CountAsync();
            var periods = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var periodIds = periods.Select(p => p.Id).ToList();

            // Load evaluation summaries for outstanding counts
            var evalCounts = await _context.KPIEvaluations
                .AsNoTracking()
                .Where(e => periodIds.Contains(e.PeriodId))
                .GroupBy(e => new { e.PeriodId, e.Status })
                .Select(g => new { g.Key.PeriodId, g.Key.Status, Count = g.Count() })
                .ToListAsync();

            var rowList = periods.Select(p =>
            {
                var periodEvals = evalCounts.Where(x => x.PeriodId == p.Id).ToList();
                return new KPIPeriodRowViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt,
                    CreatedBy = p.CreatedBy,
                    TotalEvaluations = periodEvals.Sum(x => x.Count),
                    OutstandingDraft = periodEvals.FirstOrDefault(x => x.Status == "DRAFT")?.Count ?? 0,
                    OutstandingActive = periodEvals.FirstOrDefault(x => x.Status == "ACTIVE")?.Count ?? 0,
                    OutstandingEvaluating = periodEvals.FirstOrDefault(x => x.Status == "EVALUATING")?.Count ?? 0,
                    CompletedEvaluations = periodEvals.FirstOrDefault(x => x.Status == "COMPLETED")?.Count ?? 0
                };
            }).ToList();

            var vm = new KPIPeriodListViewModel
            {
                Periods = rowList,
                StatusFilter = status,
                SearchTerm = search,
                Page = page,
                TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize))
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateKPIPeriodInput input)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = string.Join("; ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(Index));
            }

            if (input.StartDate >= input.EndDate)
            {
                TempData["ErrorMessage"] = "Ngày bắt đầu phải trước ngày kết thúc.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var period = new KPIPeriod
                {
                    Name = input.Name.Trim(),
                    StartDate = input.StartDate,
                    EndDate = input.EndDate
                };

                await _kpiService.CreatePeriodAsync(period);
                TempData["SuccessMessage"] = $"Đã tạo kỳ đánh giá '{period.Name}' thành công (Trạng thái: Dự kiến).";
            }
            catch (UnauthorizedAccessException)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền thực hiện thao tác này.";
            }
            catch (ArgumentException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Đã xảy ra lỗi không mong muốn khi tạo kỳ đánh giá.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Activate(int id)
        {
            try
            {
                await _kpiService.ActivatePeriodAsync(id);
                TempData["SuccessMessage"] = "Kỳ đánh giá đã được kích hoạt thành công.";
            }
            catch (UnauthorizedAccessException)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền thực hiện thao tác này.";
            }
            catch (KeyNotFoundException)
            {
                TempData["ErrorMessage"] = "Không tìm thấy kỳ đánh giá.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi kích hoạt kỳ đánh giá.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Close(int id)
        {
            try
            {
                var result = await _kpiService.ClosePeriodAsync(id);
                if (result.OutstandingDraft > 0 || result.OutstandingActive > 0 || result.OutstandingEvaluating > 0)
                {
                    TempData["SuccessMessage"] = $"Đã đóng kỳ đánh giá thành công. Lưu ý còn: {result.OutstandingDraft} bản nháp, {result.OutstandingActive} đang thực hiện, {result.OutstandingEvaluating} đang chấm điểm.";
                }
                else
                {
                    TempData["SuccessMessage"] = "Đã đóng kỳ đánh giá thành công.";
                }
            }
            catch (UnauthorizedAccessException)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền thực hiện thao tác này.";
            }
            catch (KeyNotFoundException)
            {
                TempData["ErrorMessage"] = "Không tìm thấy kỳ đánh giá.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi đóng kỳ đánh giá.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
