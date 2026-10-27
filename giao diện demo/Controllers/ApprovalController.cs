using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using System.Security.Claims;
using giao_dien_demo.Services;
using Microsoft.EntityFrameworkCore;

namespace giao_dien_demo.Controllers
{
    [Authorize(Roles = "HR,Leader")]
    public class ApprovalController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IApprovalEngine _approvalEngine;
        private readonly LeaveWorkflowPolicy _leavePolicy;
        private readonly AdjustmentWorkflowPolicy _adjustmentPolicy;
        private readonly ProposalWorkflowPolicy _proposalPolicy;
        private readonly ILeadershipScopeService _leadershipScope;
        private readonly NotificationEngineService _notificationEngine;
        private readonly IInAppNotificationService _notifications;

        public ApprovalController(
            ApplicationDbContext context,
            IApprovalEngine approvalEngine,
            LeaveWorkflowPolicy leavePolicy,
            AdjustmentWorkflowPolicy adjustmentPolicy,
            ProposalWorkflowPolicy proposalPolicy,
            ILeadershipScopeService leadershipScope,
            NotificationEngineService notificationEngine,
            IInAppNotificationService notifications)
        {
            _context = context;
            _approvalEngine = approvalEngine;
            _leavePolicy = leavePolicy;
            _adjustmentPolicy = adjustmentPolicy;
            _proposalPolicy = proposalPolicy;
            _leadershipScope = leadershipScope;
            _notificationEngine = notificationEngine;
            _notifications = notifications;
        }

        private string CurrentUsername => User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? HttpContext.Session.GetString("User") ?? string.Empty;

        private string CurrentRole => User.FindFirstValue(ClaimTypes.Role)
            ?? HttpContext.Session.GetString("Role") ?? string.Empty;

        private IQueryable<Proposal> ScopedProposals()
        {
            var query = _context.Proposals.AsQueryable();
            if (CurrentRole == "Leader")
            {
                var departments = _leadershipScope.GetOverseenOrganizationalUnits();
                query = query.Where(x => 
                    (x.RelatedEmployeeId != null && x.RelatedEmployee != null &&
                     x.RelatedEmployee.Department != null && departments.Contains(x.RelatedEmployee.Department))
                    ||
                    (x.RelatedEmployeeId == null && !string.IsNullOrEmpty(x.RelatedDepartment) && departments.Contains(x.RelatedDepartment))
                );
            }
            else
            {
                // HR and other roles do not have approval authority over Proposals
                query = query.Where(x => false);
            }
            return query;
        }

        public IActionResult Index(string keyword = "", string type = "", string status = "")
        {
            var role = CurrentRole;
            if (role != "HR" && role != "Leader") return Forbid();

            var items = new List<PendingApprovalItemDto>();

            // 1. Fetch Leaves ("Chờ duyệt")
            var leaves = _context.Leaves.Where(l => l.Status == "Chờ duyệt").ToList();
            foreach(var l in leaves)
            {
                var emp = l.EmployeeId.HasValue ? _context.Employees.FirstOrDefault(e => e.Id == l.EmployeeId.Value) : null;
                items.Add(new PendingApprovalItemDto
                {
                    ApprovalItemId = $"LEAVE_{l.Id}",
                    RequestId = l.Id,
                    RequestType = "Nghỉ phép",
                    RequestTypeKey = "LEAVE",
                    RequestCode = $"NP{l.Id.ToString("D4")}",
                    EmployeeName = l.EmployeeName ?? "Unknown",
                    EmployeeCode = emp?.EmployeeCode ?? "",
                    DurationSummary = $"{(l.ToDate - l.FromDate).Days + 1} ngày ({l.FromDate:dd/MM} - {l.ToDate:dd/MM})",
                    Status = "Chờ duyệt",
                    SubmittedAt = l.CreatedDate
                });
            }

            // 2. Fetch Adjustments ("PENDING")
            var adjs = _context.AttendanceAdjustmentRequests.Where(a => a.Status == "PENDING").ToList();
            foreach(var a in adjs)
            {
                var emp = _context.Employees.FirstOrDefault(e => e.EmployeeCode == a.EmployeeCode);
                items.Add(new PendingApprovalItemDto
                {
                    ApprovalItemId = $"ADJ_{a.Id}",
                    RequestId = a.Id,
                    RequestType = "Giải trình công",
                    RequestTypeKey = "ADJUSTMENT",
                    RequestCode = $"GT{a.Id.ToString("D4")}",
                    EmployeeName = emp?.Name ?? "Unknown",
                    EmployeeCode = a.EmployeeCode,
                    DurationSummary = a.Reason,
                    Status = "Chờ duyệt",
                    SubmittedAt = a.CreatedAt
                });
            }

            // 3. Fetch Proposals ("PENDING_APPROVAL" or "NEEDS_REVISION")
            var props = ScopedProposals().Where(p => p.Status == "PENDING_APPROVAL").ToList();
            foreach(var p in props)
            {
                items.Add(new PendingApprovalItemDto
                {
                    ApprovalItemId = $"PROP_{p.Id}",
                    RequestId = p.Id,
                    RequestType = "Đề xuất",
                    RequestTypeKey = "PROPOSAL",
                    RequestCode = p.ProposalCode ?? $"DX{p.Id.ToString("D4")}",
                    EmployeeName = p.RelatedEmployeeNameSnapshot ?? "Unknown",
                    EmployeeCode = "",
                    DurationSummary = p.Title ?? "",
                    Status = p.Status == "NEEDS_REVISION" ? "Yêu cầu bổ sung" : "Chờ duyệt",
                    SubmittedAt = DateTime.Now // Mock, Proposal doesn't have CreatedAt in the snippet
                });
            }

            // Calculate KPIs before filtering
            var totalPending = items.Count;
            var leavePending = items.Count(i => i.RequestTypeKey == "LEAVE");
            var adjPending = items.Count(i => i.RequestTypeKey == "ADJUSTMENT");
            var propPending = items.Count(i => i.RequestTypeKey == "PROPOSAL");

            // Apply Filters
            if (!string.IsNullOrEmpty(keyword))
            {
                keyword = keyword.ToLower();
                items = items.Where(i => i.RequestCode.ToLower().Contains(keyword) 
                                      || i.EmployeeName.ToLower().Contains(keyword)
                                      || i.EmployeeCode.ToLower().Contains(keyword)).ToList();
            }
            if (!string.IsNullOrEmpty(type))
            {
                items = items.Where(i => i.RequestTypeKey == type).ToList();
            }
            if (!string.IsNullOrEmpty(status))
            {
                items = items.Where(i => i.Status == status).ToList();
            }

            items = items.OrderByDescending(i => i.SubmittedAt).ToList();

            var vm = new PendingApprovalViewModel
            {
                TotalPending = totalPending,
                LeavePending = leavePending,
                AdjustmentPending = adjPending,
                ProposalPending = propPending,
                SearchKeyword = keyword,
                FilterType = type,
                FilterStatus = status,
                Items = items
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult GetDetail(string itemId)
        {
            var parts = itemId.Split('_');
            if (parts.Length != 2) return BadRequest();
            var type = parts[0];
            if (!int.TryParse(parts[1], out int id)) return BadRequest();

            var detail = new ApprovalDetailDto { ApprovalItemId = itemId };

            if (type == "LEAVE")
            {
                var l = _context.Leaves.FirstOrDefault(x => x.Id == id);
                if (l == null) return NotFound();
                var emp = l.EmployeeId.HasValue ? _context.Employees.FirstOrDefault(e => e.Id == l.EmployeeId.Value) : null;
                
                detail.RequestTypeKey = "LEAVE";
                detail.RequestCode = $"NP{l.Id.ToString("D4")}";
                detail.EmployeeName = l.EmployeeName ?? "";
                detail.EmployeeCode = emp?.EmployeeCode ?? "";
                detail.Department = emp?.Department ?? "";
                detail.JobTitle = emp?.JobTitle ?? "";
                detail.Status = l.Status ?? "Chờ duyệt";
                detail.SubmittedAt = l.CreatedDate;

                detail.RequestContent.Add("Loại nghỉ", "Nghỉ phép thường");
                detail.RequestContent.Add("Từ ngày", l.FromDate.ToString("dd/MM/yyyy"));
                detail.RequestContent.Add("Đến ngày", l.ToDate.ToString("dd/MM/yyyy"));
                detail.RequestContent.Add("Số ngày", $"{(l.ToDate - l.FromDate).Days + 1}");
                detail.RequestContent.Add("Lý do", l.Reason ?? "");
            }
            else if (type == "ADJ")
            {
                var a = _context.AttendanceAdjustmentRequests.FirstOrDefault(x => x.Id == id);
                if (a == null) return NotFound();
                var emp = _context.Employees.FirstOrDefault(e => e.EmployeeCode == a.EmployeeCode);

                detail.RequestTypeKey = "ADJUSTMENT";
                detail.RequestCode = $"GT{a.Id.ToString("D4")}";
                detail.EmployeeName = emp?.Name ?? "";
                detail.EmployeeCode = a.EmployeeCode;
                detail.Department = emp?.Department ?? "";
                detail.JobTitle = emp?.JobTitle ?? "";
                detail.Status = a.Status == "PENDING" ? "Chờ duyệt" : a.Status;
                detail.SubmittedAt = a.CreatedAt;

                detail.RequestContent.Add("Giờ Check-In đề xuất", a.RequestedCheckIn?.ToString(@"hh\:mm") ?? "N/A");
                detail.RequestContent.Add("Giờ Check-Out đề xuất", a.RequestedCheckOut?.ToString(@"hh\:mm") ?? "N/A");
                detail.RequestContent.Add("Lý do giải trình", a.Reason ?? "");
            }
            else if (type == "PROP")
            {
                var p = ScopedProposals().FirstOrDefault(x => x.Id == id);
                if (p == null) return NotFound();

                detail.RequestTypeKey = "PROPOSAL";
                detail.RequestCode = p.ProposalCode ?? $"DX{p.Id.ToString("D4")}";
                detail.EmployeeName = p.RelatedEmployeeNameSnapshot ?? "";
                detail.EmployeeCode = "";
                detail.Department = p.RelatedDepartment ?? "";
                detail.JobTitle = "";
                detail.Status = p.Status == "NEEDS_REVISION" ? "Yêu cầu bổ sung" : "Chờ duyệt";
                detail.SubmittedAt = DateTime.Now;

                detail.RequestContent.Add("Tiêu đề", p.Title ?? "");
                detail.RequestContent.Add("Loại đề xuất", p.ProposalType ?? "");
                detail.RequestContent.Add("Tình trạng hiện tại", p.CurrentSituation ?? "");
                detail.RequestContent.Add("Nội dung", p.ProposalContent ?? "");
                detail.RequestContent.Add("Lý do", p.Reason ?? "");
                detail.RequestContent.Add("Chi phí dự kiến", p.EstimatedCost?.ToString("N0") ?? "0");
            }
            else
            {
                return BadRequest();
            }

            return Json(detail);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(string itemId)
        {
            var parts = itemId.Split('_');
            if (parts.Length != 2) return BadRequest();
            var type = parts[0];
            if (!int.TryParse(parts[1], out int id)) return BadRequest();

            if (type == "LEAVE" || type == "ADJ")
            {
                return BadRequest("HR và Leader không có quyền duyệt đơn nghỉ phép hoặc điều chỉnh công thông thường. Thẩm quyền duyệt thuộc về Quản lý trực tiếp (Manager).");
            }
            else if (type == "PROP")
            {
                if (CurrentRole != "Leader")
                {
                    return BadRequest("Chỉ Lãnh đạo (Leader) trong phạm vi phụ trách mới có quyền phê duyệt đề xuất.");
                }
                var p = ScopedProposals().FirstOrDefault(x => x.Id == id);
                if (p != null)
                {
                    if (!string.IsNullOrEmpty(p.CreatedBy) && p.CreatedBy.Equals(CurrentUsername, StringComparison.OrdinalIgnoreCase))
                        return BadRequest("Bạn không thể tự phê duyệt đề xuất do chính mình tạo ra.");
                    if (!string.IsNullOrEmpty(p.SubmittedBy) && p.SubmittedBy.Equals(CurrentUsername, StringComparison.OrdinalIgnoreCase))
                        return BadRequest("Bạn không thể tự phê duyệt đề xuất do chính mình gửi duyệt.");

                    _notificationEngine.QueueEvent(_context, "PROPOSAL_APPROVED", p,
                        relatedUsername: p.SubmittedBy ?? p.CreatedBy,
                        idempotencyKey: $"proposal:{p.Id}:approved");
                    var result = await _approvalEngine.ProcessDecisionAsync(
                        p, "APPROVE",
                        CurrentUsername,
                        CurrentRole,
                        null, _proposalPolicy);
                    if (!result.Success) return Conflict(result.Message);
                    return Ok(new { success = true });
                }
            }
            return BadRequest("Không thể duyệt đơn này (đã bị xử lý hoặc lỗi trạng thái).");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(string itemId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return BadRequest("Vui lòng nhập lý do từ chối.");

            var parts = itemId.Split('_');
            if (parts.Length != 2) return BadRequest();
            var type = parts[0];
            if (!int.TryParse(parts[1], out int id)) return BadRequest();

            if (type == "LEAVE" || type == "ADJ")
            {
                return BadRequest("HR và Leader không có quyền từ chối đơn nghỉ phép hoặc điều chỉnh công thông thường. Thẩm quyền thuộc về Quản lý trực tiếp (Manager).");
            }
            else if (type == "PROP")
            {
                if (CurrentRole != "Leader")
                {
                    return BadRequest("Chỉ Lãnh đạo (Leader) trong phạm vi phụ trách mới có quyền từ chối đề xuất.");
                }
                var p = ScopedProposals().FirstOrDefault(x => x.Id == id);
                if (p != null)
                {
                    if (!string.IsNullOrEmpty(p.CreatedBy) && p.CreatedBy.Equals(CurrentUsername, StringComparison.OrdinalIgnoreCase))
                        return BadRequest("Bạn không thể tự xử lý đề xuất do chính mình tạo ra.");
                    if (!string.IsNullOrEmpty(p.SubmittedBy) && p.SubmittedBy.Equals(CurrentUsername, StringComparison.OrdinalIgnoreCase))
                        return BadRequest("Bạn không thể tự xử lý đề xuất do chính mình gửi duyệt.");

                    _notificationEngine.QueueEvent(_context, "PROPOSAL_REJECTED", p,
                        relatedUsername: p.SubmittedBy ?? p.CreatedBy,
                        idempotencyKey: $"proposal:{p.Id}:rejected");
                    var result = await _approvalEngine.ProcessDecisionAsync(
                        p, "REJECT",
                        CurrentUsername,
                        CurrentRole,
                        reason, _proposalPolicy);
                    if (!result.Success) return Conflict(result.Message);
                    return Ok(new { success = true });
                }
            }
            return BadRequest("Không thể từ chối đơn này.");
        }

        private async Task NotifyEmployeeAsync(int? employeeId, string message, string type, string targetUrl, int referenceId, string deduplicationKey)
        {
            if (!employeeId.HasValue) return;
            var recipient = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(x => x.EmployeeId == employeeId.Value && x.CanLogin);
            if (recipient == null) return;
            await _notifications.SendAsync(recipient.Username, message, type, targetUrl, referenceId, deduplicationKey: deduplicationKey);
        }
    }
}
