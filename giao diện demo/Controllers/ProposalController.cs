using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Services;

namespace giao_dien_demo.Controllers
{
    [Authorize(Roles = "HR,Leader")]
    public class ProposalController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IApprovalEngine _approvalEngine;
        private readonly ProposalWorkflowPolicy _proposalPolicy;
        private readonly ILeadershipScopeService _leadershipScope;
        private readonly NotificationEngineService _notificationEngine;
        private readonly IAuditLogService _auditLog;

        public ProposalController(
            ApplicationDbContext context,
            IApprovalEngine approvalEngine,
            ProposalWorkflowPolicy proposalPolicy,
            ILeadershipScopeService leadershipScope,
            NotificationEngineService notificationEngine,
            IAuditLogService auditLog)
        {
            _context = context;
            _approvalEngine = approvalEngine;
            _proposalPolicy = proposalPolicy;
            _leadershipScope = leadershipScope;
            _notificationEngine = notificationEngine;
            _auditLog = auditLog;
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
            else if (CurrentRole == "HR")
            {
                // HR has authority to view proposals
            }
            else
            {
                // Fail-closed for all other roles (Employee, Manager, Admin)
                query = query.Where(x => false);
            }
            return query;
        }

        public async Task<IActionResult> Index()
        {
            var employees = CurrentRole == "Leader"
                ? _leadershipScope.GetScopedEmployeeQuery()
                : _context.Employees.AsQueryable();
            ViewBag.Employees = await employees
                .Select(e => new { e.Id, e.Name, e.EmployeeCode, e.Department })
                .ToListAsync();
                
            ViewBag.JobPositions = await _context.JobPositions.ToListAsync();
            
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetStatistics()
        {
            var props = await ScopedProposals().ToListAsync();

            return Json(new {
                draft = props.Count(p => p.Status == "DRAFT"),
                pending = props.Count(p => p.Status == "PENDING_APPROVAL" || p.Status == "NEEDS_REVISION"),
                approved = props.Count(p => p.Status == "APPROVED"),
                rejected = props.Count(p => p.Status == "REJECTED")
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetProposals(string? type, string? status, string? search)
        {
            var query = ScopedProposals();

            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(p => p.ProposalType == type);
            }
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(p => p.Status == status);
            }
            if (!string.IsNullOrEmpty(search))
            {
                var s = search.ToLower();
                query = query.Where(p => 
                    (p.Title != null && p.Title.ToLower().Contains(s)) ||
                    (p.ProposalCode != null && p.ProposalCode.ToLower().Contains(s)) ||
                    (p.CreatedBy != null && p.CreatedBy.ToLower().Contains(s))
                );
            }

            var items = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

            var result = items.Select(p => new {
                id = p.Id,
                proposalCode = p.ProposalCode,
                title = p.Title,
                type = p.ProposalType,
                createdBy = p.CreatedBy,
                createdAt = p.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                level = p.CurrentApprovalLevel + "/" + p.TotalApprovalLevels,
                effectiveDate = p.ProposedEffectiveDate?.ToString("dd/MM/yyyy") ?? "-",
                status = p.Status,
                updatedAt = p.UpdatedAt.ToString("dd/MM/yyyy HH:mm")
            }).ToList();

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetDetail(int id)
        {
            var p = await ScopedProposals().FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return NotFound();

            var history = await _context.ProposalHistories
                .Where(h => h.ProposalId == id)
                .OrderByDescending(h => h.Timestamp)
                .Select(h => new {
                    action = h.Action,
                    actor = h.Actor,
                    details = h.Details,
                    timestamp = h.Timestamp.ToString("dd/MM/yyyy HH:mm")
                })
                .ToListAsync();

            return Json(new { proposal = p, history = history });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> SaveDraft([FromBody] Proposal model)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                return Json(new { success = false, message = "Tiêu đề không được để trống" });
            if (string.IsNullOrWhiteSpace(model.ProposalType))
                return Json(new { success = false, message = "Loại đề xuất không được để trống" });
            if (string.IsNullOrWhiteSpace(model.ProposalContent))
                return Json(new { success = false, message = "Nội dung đề xuất không được để trống" });
            if (string.IsNullOrWhiteSpace(model.Reason))
                return Json(new { success = false, message = "Lý do không được để trống" });
            if (model.EstimatedCost.HasValue && model.EstimatedCost < 0)
                return Json(new { success = false, message = "Chi phí ước tính không được âm" });

            model.ProposalCode = "PR-" + DateTime.Now.ToString("yyyyMMdd") + "-" + (new Random().Next(1000, 9999));
            model.Status = "DRAFT";
            model.CreatedAt = DateTime.Now;
            model.UpdatedAt = DateTime.Now;
            model.CreatedBy = CurrentUsername;
            
            if (model.RelatedEmployeeId.HasValue) {
                var emp = await _context.Employees.FindAsync(model.RelatedEmployeeId.Value);
                if (emp != null) model.RelatedEmployeeNameSnapshot = emp.Name;
            }
            if (model.TargetPositionId.HasValue) {
                var pos = await _context.JobPositions.FindAsync(model.TargetPositionId.Value);
                if (pos != null) model.TargetPositionSnapshot = pos.Name;
            }

            _context.Proposals.Add(model);
            await _context.SaveChangesAsync();

            _context.ProposalHistories.Add(new ProposalHistory {
                ProposalId = model.Id,
                Action = "CREATE_DRAFT",
                Actor = model.CreatedBy,
                Details = "Lưu nháp đề xuất",
                Timestamp = DateTime.Now
            });
            await _context.SaveChangesAsync();
            await _auditLog.LogAsync("CreateDraft", "Proposal", "Proposal", model.Id.ToString(), "Activity", "Success");

            return Json(new { success = true, id = model.Id, proposalCode = model.ProposalCode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> UpdateDraft([FromBody] UpdateProposalDto dto)
        {
            if (dto == null) return BadRequest();
            if (string.IsNullOrWhiteSpace(dto.Title))
                return Json(new { success = false, message = "Tiêu đề không được để trống" });
            if (string.IsNullOrWhiteSpace(dto.ProposalType))
                return Json(new { success = false, message = "Loại đề xuất không được để trống" });
            if (string.IsNullOrWhiteSpace(dto.ProposalContent))
                return Json(new { success = false, message = "Nội dung đề xuất không được để trống" });
            if (string.IsNullOrWhiteSpace(dto.Reason))
                return Json(new { success = false, message = "Lý do đề xuất không được để trống" });
            if (dto.EstimatedCost.HasValue && dto.EstimatedCost.Value < 0)
                return Json(new { success = false, message = "Chi phí dự kiến không được âm" });

            var p = await _context.Proposals.FindAsync(dto.Id);
            if (p == null) return NotFound();

            if (p.Status != "DRAFT" && p.Status != "NEEDS_REVISION")
                return Conflict(new { success = false, message = "Đề xuất chỉ có thể chỉnh sửa khi ở trạng thái Nháp hoặc Cần bổ sung." });

            p.Title = dto.Title.Trim();
            p.ProposalType = dto.ProposalType.Trim();
            p.CurrentSituation = dto.CurrentSituation;
            p.ProposalContent = dto.ProposalContent;
            p.Reason = dto.Reason;
            p.Notes = dto.Notes;
            p.EstimatedCost = dto.EstimatedCost;
            p.ProposedEffectiveDate = dto.ProposedEffectiveDate;
            p.RelatedDepartment = dto.RelatedDepartment;
            p.RelatedEmployeeId = dto.RelatedEmployeeId;

            if (dto.RelatedEmployeeId.HasValue)
            {
                var emp = await _context.Employees.FindAsync(dto.RelatedEmployeeId.Value);
                p.RelatedEmployeeNameSnapshot = emp?.Name;
            }
            else
            {
                p.RelatedEmployeeNameSnapshot = null;
            }

            if (dto.TargetPositionId.HasValue)
            {
                p.TargetPositionId = dto.TargetPositionId;
                var pos = await _context.JobPositions.FindAsync(dto.TargetPositionId.Value);
                p.TargetPositionSnapshot = pos?.Name;
            }
            else
            {
                p.TargetPositionId = null;
                p.TargetPositionSnapshot = null;
            }

            p.UpdatedAt = DateTime.Now;

            string historyAction = p.Status == "NEEDS_REVISION" ? "UPDATE_REVISION" : "UPDATE_DRAFT";
            string historyDetails = p.Status == "NEEDS_REVISION" ? "Cập nhật nội dung theo yêu cầu bổ sung" : "Cập nhật nội dung bản nháp";

            _context.ProposalHistories.Add(new ProposalHistory
            {
                ProposalId = p.Id,
                Action = historyAction,
                Actor = CurrentUsername,
                Details = historyDetails,
                Timestamp = DateTime.Now
            });

            await _context.SaveChangesAsync();
            await _auditLog.LogAsync(p.Status == "NEEDS_REVISION" ? "UpdateRevision" : "UpdateDraft", "Proposal", "Proposal", p.Id.ToString(), "Activity", "Success");

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> Submit([FromBody] dynamic data)
        {
            int id = data.id;
            var p = await _context.Proposals.FindAsync(id);
            if (p == null) return NotFound();
            if (p.Status != "DRAFT" && p.Status != "NEEDS_REVISION")
                return Conflict(new { success = false, message = "Đề xuất không ở trạng thái có thể gửi duyệt." });

            if (string.IsNullOrWhiteSpace(p.Title) || string.IsNullOrWhiteSpace(p.ProposalContent) || string.IsNullOrWhiteSpace(p.Reason))
            {
                return BadRequest(new { success = false, message = "Đề xuất còn thiếu thông tin bắt buộc (Tiêu đề, Nội dung hoặc Lý do)." });
            }

            string prevStatus = p.Status;
            p.Status = "PENDING_APPROVAL";
            p.SubmittedBy = CurrentUsername;
            p.SubmittedAt = DateTime.Now;
            p.UpdatedAt = DateTime.Now;

            _context.Proposals.Update(p);

            string historyAction = prevStatus == "NEEDS_REVISION" ? "RESUBMIT" : "SUBMIT";
            string historyDetails = prevStatus == "NEEDS_REVISION" ? "Gửi lại đề xuất sau khi bổ sung" : "Gửi đề xuất phê duyệt";

            _context.ProposalHistories.Add(new ProposalHistory {
                ProposalId = id,
                Action = historyAction,
                Actor = CurrentUsername,
                Details = historyDetails,
                Timestamp = DateTime.Now
            });

            // Phase I: Notification to scoped Leaders
            string? targetDept = null;
            if (p.RelatedEmployeeId.HasValue)
            {
                targetDept = await _context.Employees.AsNoTracking()
                    .Where(e => e.Id == p.RelatedEmployeeId.Value)
                    .Select(e => e.Department)
                    .FirstOrDefaultAsync();
            }
            if (string.IsNullOrWhiteSpace(targetDept) && !string.IsNullOrWhiteSpace(p.RelatedDepartment))
            {
                targetDept = p.RelatedDepartment.Trim();
            }

            if (!string.IsNullOrWhiteSpace(targetDept))
            {
                var now = DateTime.Now;
                var scopedLeaders = await _context.LeaderAssignments.AsNoTracking()
                    .Where(x => x.OrganizationalUnit == targetDept && x.EffectiveFrom <= now && (x.EffectiveTo == null || x.EffectiveTo > now))
                    .Include(x => x.User)
                    .Where(x => x.User != null && x.User.Role == "Leader" && x.User.CanLogin && x.User.Username != CurrentUsername)
                    .Select(x => x.User!.Username)
                    .Distinct()
                    .ToListAsync();

                foreach (var leader in scopedLeaders)
                {
                    _notificationEngine.QueueEvent(_context, "PROPOSAL_SUBMITTED", new {
                        ProposalId = p.Id,
                        ProposalCode = p.ProposalCode,
                        Title = p.Title,
                        Department = targetDept
                    }, relatedUsername: leader, idempotencyKey: $"proposal:{p.Id}:submit:{leader}:{p.UpdatedAt.Ticks}");
                }
            }

            await _context.SaveChangesAsync();
            await _auditLog.LogAsync(prevStatus == "NEEDS_REVISION" ? "Resubmit" : "Submit", "Proposal", "Proposal", id.ToString(), "Workflow", "Success");
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Leader")]
        public async Task<IActionResult> Approve([FromBody] dynamic data)
        {
            int id = data.id;
            var p = await ScopedProposals().FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return NotFound();

            if (p.Status != "PENDING_APPROVAL" && p.Status != "PENDING")
                return Conflict(new { success = false, message = "Đề xuất không ở trạng thái chờ duyệt." });

            if (!string.IsNullOrEmpty(p.CreatedBy) && p.CreatedBy.Equals(CurrentUsername, StringComparison.OrdinalIgnoreCase))
                return Forbid();
            if (!string.IsNullOrEmpty(p.SubmittedBy) && p.SubmittedBy.Equals(CurrentUsername, StringComparison.OrdinalIgnoreCase))
                return Forbid();

            _notificationEngine.QueueEvent(_context, "PROPOSAL_APPROVED", p,
                relatedUsername: p.SubmittedBy ?? p.CreatedBy,
                idempotencyKey: $"proposal:{p.Id}:approved");
            var result = await _approvalEngine.ProcessDecisionAsync(
                p, "APPROVE", CurrentUsername, CurrentRole, null, _proposalPolicy);
            if (!result.Success) return Conflict(new { success = false, message = result.Message });
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Leader")]
        public async Task<IActionResult> Reject([FromBody] dynamic data)
        {
            int id = data.id;
            string reason = data.reason;
            var p = await ScopedProposals().FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return NotFound();

            if (p.Status != "PENDING_APPROVAL" && p.Status != "PENDING")
                return Conflict(new { success = false, message = "Đề xuất không ở trạng thái chờ duyệt." });

            if (!string.IsNullOrEmpty(p.CreatedBy) && p.CreatedBy.Equals(CurrentUsername, StringComparison.OrdinalIgnoreCase))
                return Forbid();
            if (!string.IsNullOrEmpty(p.SubmittedBy) && p.SubmittedBy.Equals(CurrentUsername, StringComparison.OrdinalIgnoreCase))
                return Forbid();

            _notificationEngine.QueueEvent(_context, "PROPOSAL_REJECTED", p,
                relatedUsername: p.SubmittedBy ?? p.CreatedBy,
                idempotencyKey: $"proposal:{p.Id}:rejected");
            var result = await _approvalEngine.ProcessDecisionAsync(
                p, "REJECT", CurrentUsername, CurrentRole, reason, _proposalPolicy);
            if (!result.Success) return Conflict(new { success = false, message = result.Message });
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Leader")]
        public async Task<IActionResult> RequestRevision([FromBody] dynamic data)
        {
            int id = data.id;
            string reason = data.reason;
            var p = await ScopedProposals().FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return NotFound();

            if (p.Status != "PENDING_APPROVAL" && p.Status != "PENDING")
                return Conflict(new { success = false, message = "Đề xuất không ở trạng thái chờ duyệt." });

            if (!string.IsNullOrEmpty(p.CreatedBy) && p.CreatedBy.Equals(CurrentUsername, StringComparison.OrdinalIgnoreCase))
                return Forbid();
            if (!string.IsNullOrEmpty(p.SubmittedBy) && p.SubmittedBy.Equals(CurrentUsername, StringComparison.OrdinalIgnoreCase))
                return Forbid();

            _notificationEngine.QueueEvent(_context, "PROPOSAL_REVISION_REQUESTED", p,
                relatedUsername: p.SubmittedBy ?? p.CreatedBy,
                idempotencyKey: $"proposal:{p.Id}:revision:{(p.RowVersion == null ? "none" : Convert.ToBase64String(p.RowVersion))}");
            var result = await _approvalEngine.ProcessDecisionAsync(
                p, "REQUEST_REVISION", CurrentUsername, CurrentRole, reason, _proposalPolicy);
            if (!result.Success) return Conflict(new { success = false, message = result.Message });
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "HR")]
        public IActionResult GenerateRecord([FromBody] dynamic data)
        {
            // PROPOSAL-GEN-01: GenerateRecord has been deactivated to eliminate orphaned records and side-effects.
            return BadRequest(new { success = false, message = "Chức năng tạo tự động hồ sơ từ đề xuất đã bị vô hiệu hóa theo quy chuẩn quản trị. Vui lòng tạo hồ sơ nghiệp vụ trực tiếp trong các phân hệ tương ứng." });
        }
    }

    public class UpdateProposalDto
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? ProposalType { get; set; }
        public string? CurrentSituation { get; set; }
        public string? ProposalContent { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public int? RelatedEmployeeId { get; set; }
        public string? RelatedDepartment { get; set; }
        public int? TargetPositionId { get; set; }
        public decimal? EstimatedCost { get; set; }
        public DateTime? ProposedEffectiveDate { get; set; }
    }
}
