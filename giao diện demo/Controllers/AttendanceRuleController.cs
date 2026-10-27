using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using System.Linq;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    public class AttendanceRuleController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AttendanceRuleController(ApplicationDbContext context)
        {
            _context = context;
        }

        // VIEW: Trang chính cấu hình Quy tắc chấm công
        [Authorize(Policy = AppPolicies.HROnly)]
        public IActionResult Index()
        {
            return View();
        }

        // API: Lấy phiên bản đang Active hoặc Draft
        [HttpGet]
        public async Task<IActionResult> GetCurrentActive()
        {
            var activeVersion = await _context.AttendanceRuleVersions
                .Where(v => v.Status == "Active" || v.Status == "Draft")
                .OrderBy(v => v.Status == "Draft" ? 0 : 1) // Ưu tiên trả về Draft nếu có để user edit
                .FirstOrDefaultAsync();
                
            return Json(new { success = true, data = activeVersion });
        }

        // API: Lấy chi tiết 1 phiên bản
        [HttpGet]
        public async Task<IActionResult> GetVersion(int id)
        {
            var version = await _context.AttendanceRuleVersions.FindAsync(id);
            return Json(new { success = true, data = version });
        }

        // API: Lấy lịch sử phiên bản
        [HttpGet]
        public async Task<IActionResult> GetHistory()
        {
            var history = await _context.AttendanceRuleVersions
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();
            return Json(new { success = true, data = history });
        }

        // API: Tạo bản Draft mới từ bản Active
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> CreateDraft()
        {
            var user = CurrentActor.Username(User) ?? "System";
            
            // Check if there is already a Draft
            var existingDraft = await _context.AttendanceRuleVersions.FirstOrDefaultAsync(v => v.Status == "Draft");
            if (existingDraft != null)
            {
                return Json(new { success = false, message = "Đã tồn tại một bản Draft đang được chỉnh sửa. Vui lòng hoàn tất hoặc xóa bản đó trước." });
            }

            var activeVersion = await _context.AttendanceRuleVersions
                .Where(v => v.Status == "Active")
                .OrderByDescending(v => v.EffectiveFrom)
                .FirstOrDefaultAsync();

            var newVersionNumber = await _context.AttendanceRuleVersions.CountAsync() + 1;
            
            var newDraft = new AttendanceRuleVersion
            {
                VersionCode = $"V{newVersionNumber}",
                Status = "Draft",
                EffectiveFrom = DateTime.Now.Date.AddDays(1), // Mặc định hiệu lực từ ngày mai
                ScopeType = "Company",
                CreatedBy = user,
                CreatedAt = DateTime.Now
            };

            // Copy settings from active if exists
            if (activeVersion != null)
            {
                newDraft.LateGraceMinutes = activeVersion.LateGraceMinutes;
                newDraft.LateFullCreditThresholdMinutes = activeVersion.LateFullCreditThresholdMinutes;
                newDraft.LateHandlingMode = activeVersion.LateHandlingMode;
                newDraft.LateMaxDeductionMinutes = activeVersion.LateMaxDeductionMinutes;

                newDraft.EarlyLeaveGraceMinutes = activeVersion.EarlyLeaveGraceMinutes;
                newDraft.EarlyLeaveFullCreditThresholdMinutes = activeVersion.EarlyLeaveFullCreditThresholdMinutes;
                newDraft.EarlyLeaveHandlingMode = activeVersion.EarlyLeaveHandlingMode;
                newDraft.EarlyLeaveMaxDeductionMinutes = activeVersion.EarlyLeaveMaxDeductionMinutes;

                newDraft.CheckInRoundingMode = activeVersion.CheckInRoundingMode;
                newDraft.CheckInRoundingStepMinutes = activeVersion.CheckInRoundingStepMinutes;
                newDraft.CheckOutRoundingMode = activeVersion.CheckOutRoundingMode;
                newDraft.CheckOutRoundingStepMinutes = activeVersion.CheckOutRoundingStepMinutes;

                newDraft.MissingCheckInMode = activeVersion.MissingCheckInMode;
                newDraft.MissingCheckInAssumedTime = activeVersion.MissingCheckInAssumedTime;
                newDraft.MissingCheckOutMode = activeVersion.MissingCheckOutMode;
                newDraft.MissingCheckOutAssumedTime = activeVersion.MissingCheckOutAssumedTime;

                newDraft.DailyOtThreshold = activeVersion.DailyOtThreshold;
                newDraft.WeeklyOtThreshold = activeVersion.WeeklyOtThreshold;
                newDraft.AutoCalculateOt = activeVersion.AutoCalculateOt;
                newDraft.AllowAfterShiftOt = activeVersion.AllowAfterShiftOt;
            }

            _context.AttendanceRuleVersions.Add(newDraft);
            await _context.SaveChangesAsync();

            return Json(new { success = true, data = newDraft });
        }

        // API: Cập nhật bản Draft
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> UpdateDraft([FromBody] AttendanceRuleVersion dto)
        {
            if (dto == null || dto.Id <= 0) return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            var draft = await _context.AttendanceRuleVersions.FindAsync(dto.Id);
            if (draft == null || draft.Status != "Draft")
            {
                return Json(new { success = false, message = "Chỉ có thể cập nhật bản Draft." });
            }

            // Cross-field validation (Basic)
            if (dto.LateGraceMinutes < 0 || dto.LateMaxDeductionMinutes < 0 || dto.EarlyLeaveGraceMinutes < 0)
            {
                return Json(new { success = false, message = "Thời gian không thể là số âm." });
            }
            if (dto.DailyOtThreshold < 0 || dto.WeeklyOtThreshold < 0)
            {
                return Json(new { success = false, message = "Ngưỡng OT không hợp lệ." });
            }

            draft.EffectiveFrom = dto.EffectiveFrom;
            draft.LateGraceMinutes = dto.LateGraceMinutes;
            draft.LateFullCreditThresholdMinutes = dto.LateFullCreditThresholdMinutes;
            draft.LateHandlingMode = dto.LateHandlingMode ?? "DeductMinutes";
            draft.LateMaxDeductionMinutes = dto.LateMaxDeductionMinutes;

            draft.EarlyLeaveGraceMinutes = dto.EarlyLeaveGraceMinutes;
            draft.EarlyLeaveFullCreditThresholdMinutes = dto.EarlyLeaveFullCreditThresholdMinutes;
            draft.EarlyLeaveHandlingMode = dto.EarlyLeaveHandlingMode ?? "DeductMinutes";
            draft.EarlyLeaveMaxDeductionMinutes = dto.EarlyLeaveMaxDeductionMinutes;

            draft.CheckInRoundingMode = dto.CheckInRoundingMode ?? "None";
            draft.CheckInRoundingStepMinutes = dto.CheckInRoundingStepMinutes;
            draft.CheckOutRoundingMode = dto.CheckOutRoundingMode ?? "None";
            draft.CheckOutRoundingStepMinutes = dto.CheckOutRoundingStepMinutes;

            draft.MissingCheckInMode = dto.MissingCheckInMode ?? "RequireAdjustment";
            draft.MissingCheckInAssumedTime = dto.MissingCheckInAssumedTime;
            draft.MissingCheckOutMode = dto.MissingCheckOutMode ?? "RequireAdjustment";
            draft.MissingCheckOutAssumedTime = dto.MissingCheckOutAssumedTime;

            draft.DailyOtThreshold = dto.DailyOtThreshold;
            draft.WeeklyOtThreshold = dto.WeeklyOtThreshold;
            draft.AutoCalculateOt = dto.AutoCalculateOt;
            draft.AllowAfterShiftOt = dto.AllowAfterShiftOt;

            draft.UpdatedBy = CurrentActor.Username(User) ?? "System";
            draft.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return Json(new { success = true, data = draft });
        }

        // API: Kích hoạt bản Draft thành Active
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> Activate(int id)
        {
            var draft = await _context.AttendanceRuleVersions.FindAsync(id);
            if (draft == null || draft.Status != "Draft")
            {
                return Json(new { success = false, message = "Chỉ có thể kích hoạt bản Draft." });
            }

            var currentActives = await _context.AttendanceRuleVersions
                .Where(v => v.Status == "Active")
                .ToListAsync();

            foreach (var active in currentActives)
            {
                active.Status = "Archived";
                active.EffectiveTo = DateTime.Now;
            }

            draft.Status = "Active";
            draft.UpdatedBy = CurrentActor.Username(User) ?? "System";
            draft.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Kích hoạt phiên bản thành công!" });
        }
    }
}
