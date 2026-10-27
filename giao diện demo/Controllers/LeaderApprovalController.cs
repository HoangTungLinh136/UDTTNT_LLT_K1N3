using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Linq;
using giao_dien_demo.Models;
using System.Collections.Generic;
using System;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.LeaderOnly)]
    public class LeaderApprovalController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.ILeadershipScopeService _scopeService;

        public LeaderApprovalController(
            ApplicationDbContext context, 
            giao_dien_demo.Services.ILeadershipScopeService scopeService)
        {
            _context = context;
            _scopeService = scopeService;
        }

        public async Task<IActionResult> Index(string? tab, int processedPage = 1, int processedPageSize = 25)
        {
            processedPage = Math.Max(1, processedPage);
            processedPageSize = Math.Clamp(processedPageSize, 10, 100);
            var viewModel = new LeaderApprovalCenterViewModel
            {
                ActiveTab = string.IsNullOrWhiteSpace(tab) ? "all" : tab.Trim().ToLower(),
                ProcessedPage = processedPage,
                ProcessedPageSize = processedPageSize
            };

            var allowedDepts = _scopeService.GetOverseenOrganizationalUnits();
            if (!allowedDepts.Any())
            {
                return View(viewModel);
            }

            var currentUsername = CurrentActor.Username(User) ?? "";

            // 1. APPOINTMENT (Pending Leader)
            var pendingAppointments = await _context.AppointmentRecords
                .Include(a => a.Employee)
                .Where(a => a.ApprovalStatus == "PENDING_LEADER_APPROVAL"
                    && ((a.PreviousDepartment != null && allowedDepts.Contains(a.PreviousDepartment))
                        || (a.NewDepartment != null && allowedDepts.Contains(a.NewDepartment))))
                .OrderByDescending(a => a.SubmittedAt ?? a.CreatedAt)
                .ToListAsync();

            viewModel.AppointmentPendingCount = pendingAppointments.Count;
            foreach (var a in pendingAppointments)
            {
                viewModel.PendingItems.Add(new LeaderApprovalItemViewModel
                {
                    Id = a.Id,
                    WorkflowType = "APPOINTMENT",
                    WorkflowTypeLabel = "Bổ nhiệm",
                    WorkflowBadgeClass = "bg-primary-subtle text-primary border border-primary-subtle",
                    WorkflowIcon = "fa-solid fa-user-plus",
                    EmployeeId = a.EmployeeId,
                    EmployeeCode = a.EmployeeCodeSnapshot ?? a.Employee?.EmployeeCode ?? "---",
                    EmployeeName = a.EmployeeNameSnapshot ?? a.Employee?.Name ?? "---",
                    AvatarPath = a.Employee?.AvatarPath,
                    Department = a.PreviousDepartment,
                    CurrentPosition = a.PreviousPosition,
                    SummaryChange = $"Bổ nhiệm: {a.NewPosition} ({a.NewDepartment})",
                    Reason = a.Reason,
                    DecisionNumber = a.DecisionNumber,
                    ProposedBy = a.SubmittedBy ?? a.CreatedBy,
                    ProposedAt = a.SubmittedAt ?? a.CreatedAt,
                    EffectiveDate = a.EffectiveDate,
                    ApprovalStatus = a.ApprovalStatus,
                    ApprovalStatusLabel = "Chờ Lãnh đạo duyệt",
                    CanApprove = true,
                    ControllerName = "Appointment"
                });
            }

            // 2. DISMISSAL (Pending Leader)
            var pendingDismissals = await _context.DismissalRecords
                .Include(d => d.Employee)
                .Where(d => d.ApprovalStatus == "PENDING_LEADER_APPROVAL" && d.CurrentDepartmentSnapshot != null && allowedDepts.Contains(d.CurrentDepartmentSnapshot))
                .OrderByDescending(d => d.SubmittedAt ?? d.CreatedAt)
                .ToListAsync();

            viewModel.DismissalPendingCount = pendingDismissals.Count;
            foreach (var d in pendingDismissals)
            {
                viewModel.PendingItems.Add(new LeaderApprovalItemViewModel
                {
                    Id = d.Id,
                    WorkflowType = "DISMISSAL",
                    WorkflowTypeLabel = "Miễn nhiệm",
                    WorkflowBadgeClass = "bg-danger-subtle text-danger border border-danger-subtle",
                    WorkflowIcon = "fa-solid fa-user-minus",
                    EmployeeId = d.EmployeeId,
                    EmployeeCode = d.EmployeeCodeSnapshot ?? d.Employee?.EmployeeCode ?? "---",
                    EmployeeName = d.EmployeeNameSnapshot ?? d.Employee?.Name ?? "---",
                    AvatarPath = d.Employee?.AvatarPath,
                    Department = d.CurrentDepartmentSnapshot,
                    CurrentPosition = d.CurrentPositionSnapshot,
                    SummaryChange = $"Miễn nhiệm: {d.CurrentPositionSnapshot} ({d.PostReliefOptionLabel})",
                    Reason = d.Reason,
                    DecisionNumber = d.DecisionNumber,
                    ProposedBy = d.SubmittedBy ?? d.CreatedBy,
                    ProposedAt = d.SubmittedAt ?? d.CreatedAt,
                    EffectiveDate = d.EffectiveDate,
                    ApprovalStatus = d.ApprovalStatus,
                    ApprovalStatusLabel = "Chờ Lãnh đạo duyệt",
                    CanApprove = true,
                    ControllerName = "Dismissal"
                });
            }

            // 3. TRANSFER (Pending Leader)
            var pendingTransfers = await _context.TransferRecords
                .Include(t => t.Employee)
                .Where(t => t.ApprovalStatus == "PENDING_LEADER_APPROVAL"
                    && ((t.CurrentDepartmentSnapshot != null && allowedDepts.Contains(t.CurrentDepartmentSnapshot))
                        || (t.TargetDepartment != null && allowedDepts.Contains(t.TargetDepartment))))
                .OrderByDescending(t => t.SubmittedAt ?? t.CreatedAt)
                .ToListAsync();

            viewModel.TransferPendingCount = pendingTransfers.Count;
            foreach (var t in pendingTransfers)
            {
                viewModel.PendingItems.Add(new LeaderApprovalItemViewModel
                {
                    Id = t.Id,
                    WorkflowType = "TRANSFER",
                    WorkflowTypeLabel = "Điều chuyển",
                    WorkflowBadgeClass = "bg-warning-subtle text-warning border border-warning-subtle",
                    WorkflowIcon = "fa-solid fa-right-left",
                    EmployeeId = t.EmployeeId,
                    EmployeeCode = t.EmployeeCodeSnapshot ?? t.Employee?.EmployeeCode ?? "---",
                    EmployeeName = t.EmployeeNameSnapshot ?? t.Employee?.Name ?? "---",
                    AvatarPath = t.Employee?.AvatarPath,
                    Department = t.CurrentDepartmentSnapshot,
                    CurrentPosition = t.CurrentPositionSnapshot,
                    SummaryChange = $"Điều chuyển: {t.CurrentDepartmentSnapshot} → {t.TargetDepartment} ({t.TargetPosition})",
                    Reason = t.Reason,
                    DecisionNumber = t.DecisionNumber,
                    ProposedBy = t.SubmittedBy ?? t.CreatedBy,
                    ProposedAt = t.SubmittedAt ?? t.CreatedAt,
                    EffectiveDate = t.EffectiveDate,
                    ApprovalStatus = t.ApprovalStatus,
                    ApprovalStatusLabel = "Chờ Lãnh đạo duyệt",
                    CanApprove = true,
                    ControllerName = "Transfer"
                });
            }

            // 4. REWARD (Pending Leader)
            var pendingRewards = await _context.RewardRecords
                .Include(r => r.Employee)
                .Where(r => r.ApprovalStatus == "PENDING_LEADER" && r.DepartmentSnapshot != null && allowedDepts.Contains(r.DepartmentSnapshot))
                .OrderByDescending(r => r.SubmittedAt ?? r.ProposedDate)
                .ToListAsync();

            viewModel.RewardPendingCount = pendingRewards.Count;
            foreach (var r in pendingRewards)
            {
                bool isSelf = !string.IsNullOrEmpty(r.ProposedBy) && r.ProposedBy == currentUsername;
                viewModel.PendingItems.Add(new LeaderApprovalItemViewModel
                {
                    Id = r.Id,
                    WorkflowType = "REWARD",
                    WorkflowTypeLabel = "Khen thưởng",
                    WorkflowBadgeClass = "bg-success-subtle text-success border border-success-subtle",
                    WorkflowIcon = "fa-solid fa-award",
                    EmployeeId = r.EmployeeId,
                    EmployeeCode = r.EmployeeCodeSnapshot ?? r.Employee?.EmployeeCode ?? "---",
                    EmployeeName = r.EmployeeNameSnapshot ?? r.Employee?.Name ?? "---",
                    AvatarPath = r.Employee?.AvatarPath,
                    Department = r.DepartmentSnapshot ?? "---",
                    CurrentPosition = r.PositionSnapshot ?? "---",
                    SummaryChange = $"Khen thưởng: {r.Title} - {r.RewardType}" + (r.HasMonetaryReward ? $" ({r.RewardAmount:N0} VNĐ)" : ""),
                    Reason = r.Achievement,
                    DecisionNumber = r.DecisionNumber,
                    ProposedBy = r.ProposedBy,
                    ProposedAt = r.SubmittedAt ?? r.ProposedDate,
                    EffectiveDate = r.EffectiveDate,
                    ApprovalStatus = r.ApprovalStatus,
                    ApprovalStatusLabel = "Chờ Lãnh đạo duyệt",
                    CanApprove = !isSelf,
                    SelfApprovalNotice = isSelf ? "Bạn là người đề xuất khen thưởng này. Không được tự phê duyệt." : null,
                    ControllerName = "Reward"
                });
            }

            // 5. PROPOSAL (HR submitted -> Leader decision)
            var pendingProposals = await _context.Proposals
                .Include(p => p.RelatedEmployee)
                .Where(p => p.Status == "PENDING_APPROVAL"
                    && ((p.RelatedEmployeeId != null && p.RelatedEmployee != null && p.RelatedEmployee.Department != null && allowedDepts.Contains(p.RelatedEmployee.Department))
                        || (p.RelatedEmployeeId == null && p.RelatedDepartment != null && allowedDepts.Contains(p.RelatedDepartment))))
                .OrderByDescending(p => p.SubmittedAt ?? p.CreatedAt)
                .ToListAsync();
            viewModel.ProposalPendingCount = pendingProposals.Count;
            foreach (var p in pendingProposals)
            {
                var employeeCode = p.RelatedEmployee?.EmployeeCode ?? "---";
                var employeeName = p.RelatedEmployee?.Name ?? p.RelatedEmployeeNameSnapshot ?? "---";
                var department = p.RelatedEmployee?.Department ?? p.RelatedDepartment ?? "---";
                viewModel.PendingItems.Add(new LeaderApprovalItemViewModel
                {
                    Id = p.Id,
                    WorkflowType = "PROPOSAL",
                    WorkflowTypeLabel = "Đề xuất",
                    WorkflowBadgeClass = "bg-info-subtle text-info border border-info-subtle",
                    WorkflowIcon = "fa-solid fa-lightbulb",
                    EmployeeId = p.RelatedEmployeeId,
                    EmployeeCode = employeeCode,
                    EmployeeName = employeeName,
                    AvatarPath = p.RelatedEmployee?.AvatarPath,
                    Department = department,
                    CurrentPosition = p.TargetPositionSnapshot,
                    SummaryChange = p.Title ?? p.ProposalContent ?? "Đề xuất chủ trương",
                    Reason = p.Reason,
                    ProposedBy = p.SubmittedBy ?? p.CreatedBy,
                    ProposedAt = p.SubmittedAt ?? p.CreatedAt,
                    EffectiveDate = p.ProposedEffectiveDate,
                    ApprovalStatus = p.Status,
                    ApprovalStatusLabel = "Chờ Lãnh đạo quyết định",
                    CanApprove = !(!string.IsNullOrWhiteSpace(p.CreatedBy) && p.CreatedBy.Equals(currentUsername, StringComparison.OrdinalIgnoreCase))
                        && !(!string.IsNullOrWhiteSpace(p.SubmittedBy) && p.SubmittedBy.Equals(currentUsername, StringComparison.OrdinalIgnoreCase)),
                    SelfApprovalNotice = "Bạn là người tạo/gửi đề xuất này. Không thể tự xử lý.",
                    ControllerName = "Approval"
                });
            }

            // Sort pending items by ProposedAt descending
            viewModel.PendingItems = viewModel.PendingItems.OrderByDescending(i => i.ProposedAt).ToList();

            // 5. PROCESSED ITEMS (Đã xử lý gần đây)
            var processedAppointments = await _context.AppointmentRecords
                .Include(a => a.Employee)
                .Where(a => (a.ApprovalStatus == "APPROVED" || a.ApprovalStatus == "REJECTED" || a.ApprovalStatus == "EFFECTIVE")
                    && ((a.PreviousDepartment != null && allowedDepts.Contains(a.PreviousDepartment))
                        || (a.NewDepartment != null && allowedDepts.Contains(a.NewDepartment))))
                .OrderByDescending(a => a.ApprovedAt ?? a.RejectedAt ?? a.CreatedAt)
                .Select(a => new LeaderApprovalItemViewModel
                {
                    Id = a.Id,
                    WorkflowType = "APPOINTMENT",
                    WorkflowTypeLabel = "Bổ nhiệm",
                    WorkflowBadgeClass = "bg-primary-subtle text-primary border border-primary-subtle",
                    WorkflowIcon = "fa-solid fa-user-plus",
                    EmployeeId = a.EmployeeId,
                    EmployeeCode = a.EmployeeCodeSnapshot ?? "---",
                    EmployeeName = a.EmployeeNameSnapshot ?? "---",
                    AvatarPath = a.Employee != null ? a.Employee.AvatarPath : null,
                    Department = a.PreviousDepartment,
                    CurrentPosition = a.PreviousPosition,
                    SummaryChange = $"Bổ nhiệm: {a.NewPosition} ({a.NewDepartment})",
                    Reason = a.Reason,
                    DecisionNumber = a.DecisionNumber,
                    ProposedBy = a.SubmittedBy ?? a.CreatedBy,
                    ProposedAt = a.SubmittedAt ?? a.CreatedAt,
                    EffectiveDate = a.EffectiveDate,
                    ApprovalStatus = a.ApprovalStatus,
                    ApprovalStatusLabel = a.ApprovalStatus == "REJECTED" ? "Từ chối" : (a.ApprovalStatus == "EFFECTIVE" ? "Có hiệu lực" : "Đã duyệt"),
                    CanApprove = false,
                    ControllerName = "Appointment"
                })
                .ToListAsync();

            var processedDismissals = await _context.DismissalRecords
                .Include(d => d.Employee)
                .Where(d => (d.ApprovalStatus == "APPROVED" || d.ApprovalStatus == "REJECTED" || d.ApprovalStatus == "EFFECTIVE") && d.CurrentDepartmentSnapshot != null && allowedDepts.Contains(d.CurrentDepartmentSnapshot))
                .OrderByDescending(d => d.ApprovedAt ?? d.RejectedAt ?? d.CreatedAt)
                .Select(d => new LeaderApprovalItemViewModel
                {
                    Id = d.Id,
                    WorkflowType = "DISMISSAL",
                    WorkflowTypeLabel = "Miễn nhiệm",
                    WorkflowBadgeClass = "bg-danger-subtle text-danger border border-danger-subtle",
                    WorkflowIcon = "fa-solid fa-user-minus",
                    EmployeeId = d.EmployeeId,
                    EmployeeCode = d.EmployeeCodeSnapshot ?? "---",
                    EmployeeName = d.EmployeeNameSnapshot ?? "---",
                    AvatarPath = d.Employee != null ? d.Employee.AvatarPath : null,
                    Department = d.CurrentDepartmentSnapshot,
                    CurrentPosition = d.CurrentPositionSnapshot,
                    SummaryChange = $"Miễn nhiệm: {d.CurrentPositionSnapshot} ({d.PostReliefOptionLabel})",
                    Reason = d.Reason,
                    DecisionNumber = d.DecisionNumber,
                    ProposedBy = d.SubmittedBy ?? d.CreatedBy,
                    ProposedAt = d.SubmittedAt ?? d.CreatedAt,
                    EffectiveDate = d.EffectiveDate,
                    ApprovalStatus = d.ApprovalStatus,
                    ApprovalStatusLabel = d.ApprovalStatus == "REJECTED" ? "Từ chối" : (d.ApprovalStatus == "EFFECTIVE" ? "Có hiệu lực" : "Đã duyệt"),
                    CanApprove = false,
                    ControllerName = "Dismissal"
                })
                .ToListAsync();

            var processedTransfers = await _context.TransferRecords
                .Include(t => t.Employee)
                .Where(t => (t.ApprovalStatus == "APPROVED" || t.ApprovalStatus == "REJECTED" || t.ApprovalStatus == "EFFECTIVE")
                    && ((t.CurrentDepartmentSnapshot != null && allowedDepts.Contains(t.CurrentDepartmentSnapshot))
                        || (t.TargetDepartment != null && allowedDepts.Contains(t.TargetDepartment))))
                .OrderByDescending(t => t.ApprovedAt ?? t.RejectedAt ?? t.CreatedAt)
                .Select(t => new LeaderApprovalItemViewModel
                {
                    Id = t.Id,
                    WorkflowType = "TRANSFER",
                    WorkflowTypeLabel = "Điều chuyển",
                    WorkflowBadgeClass = "bg-warning-subtle text-warning border border-warning-subtle",
                    WorkflowIcon = "fa-solid fa-right-left",
                    EmployeeId = t.EmployeeId,
                    EmployeeCode = t.EmployeeCodeSnapshot ?? "---",
                    EmployeeName = t.EmployeeNameSnapshot ?? "---",
                    AvatarPath = t.Employee != null ? t.Employee.AvatarPath : null,
                    Department = t.CurrentDepartmentSnapshot,
                    CurrentPosition = t.CurrentPositionSnapshot,
                    SummaryChange = $"Điều chuyển: {t.CurrentDepartmentSnapshot} → {t.TargetDepartment} ({t.TargetPosition})",
                    Reason = t.Reason,
                    DecisionNumber = t.DecisionNumber,
                    ProposedBy = t.SubmittedBy ?? t.CreatedBy,
                    ProposedAt = t.SubmittedAt ?? t.CreatedAt,
                    EffectiveDate = t.EffectiveDate,
                    ApprovalStatus = t.ApprovalStatus,
                    ApprovalStatusLabel = t.ApprovalStatus == "REJECTED" ? "Từ chối" : (t.ApprovalStatus == "EFFECTIVE" ? "Có hiệu lực" : "Đã duyệt"),
                    CanApprove = false,
                    ControllerName = "Transfer"
                })
                .ToListAsync();

            var processedRewards = await _context.RewardRecords
                .Include(r => r.Employee)
                .Where(r => (r.ApprovalStatus == "APPROVED" || r.ApprovalStatus == "REJECTED") && r.DepartmentSnapshot != null && allowedDepts.Contains(r.DepartmentSnapshot))
                .OrderByDescending(r => r.ApprovedAt ?? r.RejectedAt ?? r.ProposedDate)
                .Select(r => new LeaderApprovalItemViewModel
                {
                    Id = r.Id,
                    WorkflowType = "REWARD",
                    WorkflowTypeLabel = "Khen thưởng",
                    WorkflowBadgeClass = "bg-success-subtle text-success border border-success-subtle",
                    WorkflowIcon = "fa-solid fa-award",
                    EmployeeId = r.EmployeeId,
                    EmployeeCode = r.EmployeeCodeSnapshot ?? "---",
                    EmployeeName = r.EmployeeNameSnapshot ?? "---",
                    AvatarPath = r.Employee != null ? r.Employee.AvatarPath : null,
                    Department = r.DepartmentSnapshot ?? "---",
                    CurrentPosition = r.PositionSnapshot ?? "---",
                    SummaryChange = $"Khen thưởng: {r.Title} - {r.RewardType}" + (r.HasMonetaryReward ? $" ({r.RewardAmount:N0} VNĐ)" : ""),
                    Reason = r.Achievement,
                    DecisionNumber = r.DecisionNumber,
                    ProposedBy = r.ProposedBy,
                    ProposedAt = r.SubmittedAt ?? r.ProposedDate,
                    EffectiveDate = r.EffectiveDate,
                    ApprovalStatus = r.ApprovalStatus,
                    ApprovalStatusLabel = r.ApprovalStatus == "REJECTED" ? "Từ chối" : "Đã duyệt",
                    CanApprove = false,
                    ControllerName = "Reward"
                })
                .ToListAsync();

            var processedProposals = await _context.Proposals
                .Include(p => p.RelatedEmployee)
                .Where(p => (p.Status == "APPROVED" || p.Status == "REJECTED" || p.Status == "NEEDS_REVISION")
                    && ((p.RelatedEmployeeId != null && p.RelatedEmployee != null && p.RelatedEmployee.Department != null && allowedDepts.Contains(p.RelatedEmployee.Department))
                        || (p.RelatedEmployeeId == null && p.RelatedDepartment != null && allowedDepts.Contains(p.RelatedDepartment))))
                .OrderByDescending(p => p.ApprovedAt ?? p.RejectedAt ?? p.RevisionRequestedAt ?? p.UpdatedAt)
                .Select(p => new LeaderApprovalItemViewModel
                {
                    Id = p.Id,
                    WorkflowType = "PROPOSAL",
                    WorkflowTypeLabel = "Đề xuất",
                    WorkflowBadgeClass = "bg-info-subtle text-info border border-info-subtle",
                    WorkflowIcon = "fa-solid fa-lightbulb",
                    EmployeeId = p.RelatedEmployeeId,
                    EmployeeCode = p.RelatedEmployee != null ? p.RelatedEmployee.EmployeeCode : "---",
                    EmployeeName = p.RelatedEmployee != null ? p.RelatedEmployee.Name : p.RelatedEmployeeNameSnapshot,
                    AvatarPath = p.RelatedEmployee != null ? p.RelatedEmployee.AvatarPath : null,
                    Department = p.RelatedEmployee != null ? p.RelatedEmployee.Department : p.RelatedDepartment,
                    CurrentPosition = p.TargetPositionSnapshot,
                    SummaryChange = p.Title ?? p.ProposalContent ?? "Đề xuất chủ trương",
                    Reason = p.Reason,
                    ProposedBy = p.SubmittedBy ?? p.CreatedBy,
                    ProposedAt = p.SubmittedAt ?? p.CreatedAt,
                    EffectiveDate = p.ProposedEffectiveDate,
                    ApprovalStatus = p.Status,
                    ApprovalStatusLabel = p.Status == "REJECTED" ? "Từ chối" : (p.Status == "NEEDS_REVISION" ? "Yêu cầu bổ sung" : "Đã duyệt"),
                    CanApprove = false,
                    ControllerName = "Approval"
                }).ToListAsync();

            var allProcessedItems = processedAppointments
                .Concat(processedDismissals)
                .Concat(processedTransfers)
                .Concat(processedRewards)
                .Concat(processedProposals)
                .OrderByDescending(i => i.ProposedAt)
                .ToList();

            viewModel.ProcessedCount = allProcessedItems.Count;
            viewModel.ProcessedItems = allProcessedItems
                .Skip((processedPage - 1) * processedPageSize)
                .Take(processedPageSize)
                .ToList();

            return View(viewModel);
        }
    }
}
