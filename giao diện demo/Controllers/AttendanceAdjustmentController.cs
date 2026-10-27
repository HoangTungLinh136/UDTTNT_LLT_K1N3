using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;

using giao_dien_demo.Services;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize]
    public class AttendanceAdjustmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAttendanceCalculationService _attendanceCalc;
        private readonly giao_dien_demo.Services.IAuditLogService _auditLogService;
        private readonly IProtectedFileService _fileService;
        private readonly giao_dien_demo.Services.IApprovalEngine _approvalEngine;
        private readonly giao_dien_demo.Services.AdjustmentWorkflowPolicy _adjustmentPolicy;
        private readonly IInAppNotificationService _notifications;

        public AttendanceAdjustmentController(
            ApplicationDbContext context,
            IAttendanceCalculationService attendanceCalc,
            giao_dien_demo.Services.IAuditLogService auditLogService,
            IProtectedFileService fileService,
            giao_dien_demo.Services.IApprovalEngine approvalEngine,
            giao_dien_demo.Services.AdjustmentWorkflowPolicy adjustmentPolicy,
            IInAppNotificationService notifications)
        {
            _context = context;
            _attendanceCalc = attendanceCalc;
            _auditLogService = auditLogService;
            _fileService = fileService;
            _approvalEngine = approvalEngine;
            _adjustmentPolicy = adjustmentPolicy;
            _notifications = notifications;
        }

        private bool CheckLogin()
        {
            return HttpContext.Session.GetString("User") != null;
        }


        // --- NHÂN VIÊN GỬI YÊU CẦU ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRequest(int attendanceId, string? reqCheckIn, string? reqCheckOut, string reason, List<IFormFile> files)
        {
            if (!CheckLogin()) return Json(new { success = false, message = "Vui lòng đăng nhập" });

            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");

            if (string.IsNullOrEmpty(reason))
                return Json(new { success = false, message = "Lý do không được để trống" });

            var attendance = await _context.Attendances.FindAsync(attendanceId);
            if (attendance == null)
                return Json(new { success = false, message = "Không tìm thấy chấm công" });

            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                // Check if there is already a PENDING request for this attendance
                bool hasPending = await _context.AttendanceAdjustmentRequests
                    .AnyAsync(r => r.AttendanceId == attendanceId && r.Status == "PENDING");
                if (hasPending)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "Đã có yêu cầu điều chỉnh đang chờ duyệt cho lượt chấm công này." });
                }

            // Check for CLOSED Attendance Period
            int m = attendance.CheckIn.Month;
            int y = attendance.CheckIn.Year;
            var period = await _context.AttendancePeriods.FirstOrDefaultAsync(p => p.Year == y && p.Month == m);
            if (period != null && period.Status == "CLOSED")
            {
                return Json(new { success = false, message = "Không thể điều chỉnh kỳ công đã đóng." });
            }

            var employee = attendance.EmployeeId.HasValue ? await _context.Employees.FindAsync(attendance.EmployeeId.Value) : null;
            if (employee == null) return Forbid();
            string? empCode = employee.EmployeeCode;

            if (role != "HR" && role != "Leader")
            {
                var currentUser = await CurrentActor.ResolveUserAsync(User, _context);
                if (currentUser?.EmployeeId == null || attendance.EmployeeId != currentUser.EmployeeId.Value)
                    return Json(new { success = false, message = "Bạn không có quyền yêu cầu điều chỉnh cho người khác" });
            }

            TimeSpan? tIn = null;
            TimeSpan? tOut = null;
            if (!string.IsNullOrEmpty(reqCheckIn) && TimeSpan.TryParse(reqCheckIn, out var cIn)) tIn = cIn;
            if (!string.IsNullOrEmpty(reqCheckOut) && TimeSpan.TryParse(reqCheckOut, out var cOut)) tOut = cOut;

            if (tIn == null && tOut == null)
                return Json(new { success = false, message = "Phải nhập ít nhất thời gian Check-in hoặc Check-out đề xuất" });

            if (tIn.HasValue && tOut.HasValue && tOut.Value <= tIn.Value)
                return Json(new { success = false, message = "Thời gian Check-out phải sau Check-in" });

            var request = new AttendanceAdjustmentRequest
            {
                AttendanceId = attendanceId,
                EmployeeCode = empCode ?? attendance.EmployeeCode,
                RequestedCheckIn = tIn,
                RequestedCheckOut = tOut,
                Reason = reason,
                Status = "PENDING",
                CreatedAt = DateTime.Now,
                ReviewedBy = null,
                ReviewedAt = null
            };

            _context.AttendanceAdjustmentRequests.Add(request);
            await _context.SaveChangesAsync();

            // Lưu file đính kèm
            if (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    if (file.Length > 0)
                    {
                        if (file.Length > 5 * 1024 * 1024) continue; // max 5MB
                        var ext = Path.GetExtension(file.FileName).ToLower();
                        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".pdf") continue;

                        var (canonicalId, _) = await _fileService.SaveProtectedFileAsync("AttendanceAdjustment", request.Id.ToString(), file);

                        var attachment = new AttendanceAdjustmentAttachment
                        {
                            RequestId = request.Id,
                            FileName = file.FileName,
                            FilePath = canonicalId,
                            ContentType = file.ContentType,
                            UploadedAt = DateTime.Now
                        };
                        _context.AttendanceAdjustmentAttachments.Add(attachment);
                    }
                }
                await _context.SaveChangesAsync();
            }

            // Gửi thông báo tới Manager(s) phụ trách bộ phận của nhân viên
            if (!string.IsNullOrEmpty(employee.Department))
            {
                var now = DateTime.Now;
                var managerUserIds = await _context.ManagerAssignments
                    .Where(a => a.OrganizationalUnit == employee.Department && a.EffectiveFrom <= now && (a.EffectiveTo == null || a.EffectiveTo > now))
                    .Select(a => a.UserId)
                    .Distinct()
                    .ToListAsync();
                var managers = await _context.Users.Where(u => managerUserIds.Contains(u.Id) && u.CanLogin).ToListAsync();
                var notificationMessage = $"🔔 Nhân viên {attendance.EmployeeName} gửi yêu cầu điều chỉnh chấm công ngày {attendance.CheckIn:dd/MM/yyyy}.";
                var notificationUrl = $"/ManagerWorkspace/AttendanceAdjustments";
                foreach (var mgr in managers)
                {
                    await _notifications.SendAsync(mgr.Username, notificationMessage, "ATTENDANCE_ADJUSTMENT_REQUEST", notificationUrl, request.Id,
                        deduplicationKey: $"attendance-adjustment-request:{request.Id}:{mgr.Id}");
                }
            }

                await transaction.CommitAsync();
                return Json(new { success = true, message = "Đã gửi yêu cầu thành công" });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                return Json(new { success = false, message = "Dữ liệu đã bị thay đổi bởi người khác. Vui lòng thử lại." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Đã xảy ra lỗi trong quá trình xử lý." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadAttachment(int attachmentId)
        {
            var attachment = await _context.AttendanceAdjustmentAttachments.FindAsync(attachmentId);
            if (attachment == null) return NotFound();

            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            var request = await _context.AttendanceAdjustmentRequests.FindAsync(attachment.RequestId);
            if (role != "HR" && role != "Leader")
            {
                 var attendance = await _context.Attendances.FindAsync(request.AttendanceId);
                 var currentUser = await CurrentActor.ResolveUserAsync(User, _context);
                 if (attendance == null || currentUser?.EmployeeId == null || attendance.EmployeeId != currentUser.EmployeeId.Value) return Forbid();
            }
            else if (role == "Leader")
            {
                 var attendance = await _context.Attendances.FindAsync(request.AttendanceId);
                 var emp = await _context.Employees.FindAsync(attendance?.EmployeeId);
                 var scopeService = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.ILeadershipScopeService>();
                 if (emp == null || !scopeService.GetOverseenOrganizationalUnits().Contains(emp.Department)) return Forbid();
            }

            try
            {
                var (physicalPath, contentType, originalName) = await _fileService.GetProtectedFileAsync(attachment.FilePath);
                return PhysicalFile(physicalPath, attachment.ContentType ?? contentType, attachment.FileName ?? originalName);
            }
            catch (Exception)
            {
                return NotFound("File is missing or access denied.");
            }
        }

        // --- GIAO DIỆN QUẢN LÝ HR ---
        public async Task<IActionResult> Manage(string? employee, string? date, string? status, string? handler, int? highlightId)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            if (role != "HR" && role != "Leader") return Forbid();

            var query = _context.AttendanceAdjustmentRequests.AsQueryable();

            if (role == "Leader")
            {
                var scopeService = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.ILeadershipScopeService>();
                var leaderDepts = scopeService.GetOverseenOrganizationalUnits();
                // We need to filter requests by employees in leaderDepts
                var empCodesInDepts = _context.Employees.Where(e => leaderDepts.Contains(e.Department)).Select(e => e.EmployeeCode).ToList();
                query = query.Where(q => empCodesInDepts.Contains(q.EmployeeCode));
            }

            if (!string.IsNullOrEmpty(employee))
            {
                query = query.Where(q => q.EmployeeCode.Contains(employee));
            }
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(q => q.Status == status);
            }
            if (!string.IsNullOrEmpty(handler))
            {
                query = query.Where(q => q.ReviewedBy != null && q.ReviewedBy.Contains(handler));
            }
            // date filter can be added if we join with Attendances

            var requests = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

            // Calculate KPIs
            ViewBag.TotalPending = await _context.AttendanceAdjustmentRequests.CountAsync(r => r.Status == "PENDING");
            ViewBag.TotalApproved = await _context.AttendanceAdjustmentRequests.CountAsync(r => r.Status == "APPROVED");
            ViewBag.TotalRejected = await _context.AttendanceAdjustmentRequests.CountAsync(r => r.Status == "REJECTED");
            ViewBag.TotalRevision = await _context.AttendanceAdjustmentRequests.CountAsync(r => r.Status == "NEEDS_REVISION");

            ViewBag.HighlightId = highlightId;
            return View(requests);
        }

        [HttpGet]
        public async Task<IActionResult> GetDetails(int id)
        {
            if (!CheckLogin()) return Unauthorized();

            var request = await _context.AttendanceAdjustmentRequests.FindAsync(id);
            if (request == null) return NotFound();

            var attendance = await _context.Attendances.FindAsync(request.AttendanceId);
            if (attendance == null) return NotFound();

            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            var currentUser = await CurrentActor.ResolveUserAsync(User, _context);
            if (role != "HR" && role != "Leader" && (currentUser?.EmployeeId == null || attendance.EmployeeId != currentUser.EmployeeId.Value))
            {
                return Forbid();
            }
            if (role == "Leader")
            {
                var emp = await _context.Employees.FindAsync(attendance.EmployeeId);
                var scopeService = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.ILeadershipScopeService>();
                if (emp == null || !scopeService.GetOverseenOrganizationalUnits().Contains(emp.Department)) return Forbid();
            }

            var attachments = await _context.AttendanceAdjustmentAttachments.Where(a => a.RequestId == id).ToListAsync();

            return Json(new {
                request = request,
                attendance = attendance,
                attachments = attachments
            });
        }

        // --- DUYỆT / TỪ CHỐI (MANAGER ONLY) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewRequest(int id, string actionType, string? rejectionReason)
        {
            if (!CheckLogin()) return Json(new { success = false, message = "Vui lòng đăng nhập" });
            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            var username = (await CurrentActor.ResolveUserAsync(User, _context))?.Username ?? HttpContext.Session.GetString("User") ?? "";
            if (role != AppRoles.Manager) return Json(new { success = false, message = "Chỉ Quản lý trực tiếp (Manager) mới có quyền duyệt yêu cầu điều chỉnh công." });

            var request = await _context.AttendanceAdjustmentRequests.FindAsync(id);
            if (request == null) return Json(new { success = false, message = "Yêu cầu không tồn tại" });

            var attendance = await _context.Attendances.FindAsync(request.AttendanceId);
            if (attendance == null) return Json(new { success = false, message = "Bản ghi chấm công không tồn tại" });

            var (success, message) = await _approvalEngine.ProcessDecisionAsync(request, actionType, username, role, rejectionReason, _adjustmentPolicy);

            if (success)
            {
                // Notify employee
                var targetEmpId = attendance.EmployeeId;
                if (!targetEmpId.HasValue && !string.IsNullOrEmpty(request.EmployeeCode))
                {
                    targetEmpId = await _context.Employees
                        .Where(e => e.EmployeeCode == request.EmployeeCode)
                        .Select(e => (int?)e.Id)
                        .FirstOrDefaultAsync();
                }
                var nNhan = targetEmpId.HasValue
                    ? await _context.Users
                        .Where(u => u.EmployeeId == targetEmpId.Value && u.CanLogin)
                        .Select(u => u.Username)
                        .FirstOrDefaultAsync()
                    : null;
                
                string noiDung = "";
                if (actionType == "APPROVE")
                    noiDung = $"✅ Yêu cầu điều chỉnh chấm công ngày {attendance.CheckIn:dd/MM/yyyy} đã được duyệt.";
                else if (actionType == "REJECT")
                    noiDung = $"❌ Yêu cầu điều chỉnh chấm công ngày {attendance.CheckIn:dd/MM/yyyy} bị từ chối. Lý do: {rejectionReason}";
                else if (actionType == "REQUEST_REVISION")
                    noiDung = $"⚠️ Yêu cầu xác nhận công ngày {attendance.CheckIn:dd/MM/yyyy} cần bổ sung thông tin. Lý do: {rejectionReason}";

                if (!string.IsNullOrWhiteSpace(nNhan))
                {
                    await _notifications.SendAsync(
                        nNhan, noiDung, "ATTENDANCE_ADJUSTMENT_RESULT",
                        $"/Attendance/Index?requestId={request.Id}", request.Id,
                        deduplicationKey: $"attendance-adjustment-result:{request.Id}:{actionType}");
                }

                return Json(new { success = true, message = "Xử lý thành công!" });
            }
            else
            {
                return Json(new { success = false, message = message });
            }
        }

        private double CalculateOverlap(DateTime start1, DateTime end1, DateTime start2, DateTime end2)
        {
            var maxStart = start1 > start2 ? start1 : start2;
            var minEnd = end1 < end2 ? end1 : end2;
            var overlap = (minEnd - maxStart).TotalHours;
            return overlap > 0 ? overlap : 0;
        }

        [HttpGet]
        public IActionResult ExportExcel()
        {
            if (!CheckLogin()) return Unauthorized();
            var role = HttpContext.Session.GetString("Role");
            if (role != "HR" && role != "Leader") return Forbid();

            // Mock export logic for now
            return File(new byte[0], "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "XacNhanCong.xlsx");
        }
    }
}
