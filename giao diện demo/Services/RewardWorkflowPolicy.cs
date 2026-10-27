using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using Microsoft.AspNetCore.SignalR;
using giao_dien_demo.Hubs;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Services
{
    public class RewardWorkflowPolicy : IWorkflowPolicy<RewardRecord>
    {
        public string DomainName => "Reward";
        private readonly IProtectedFileService _fileService;
        private readonly IHubContext<DashboardHub> _hubContext;
        private readonly ILeadershipScopeService _leadershipScope;

        public RewardWorkflowPolicy(
            IProtectedFileService fileService, 
            IHubContext<DashboardHub> hubContext,
            ILeadershipScopeService leadershipScope)
        {
            _fileService = fileService;
            _hubContext = hubContext;
            _leadershipScope = leadershipScope;
        }

        public async Task<bool> IsEligibleApproverAsync(RewardRecord entity, string approverUsername, string approverRole, ApplicationDbContext context)
        {
            // 1. Prevent Self-Approval
            if (!string.IsNullOrEmpty(entity.ProposedBy) && entity.ProposedBy.Equals(approverUsername, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var approver = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == approverUsername && u.CanLogin);
            if (approver?.EmployeeId == entity.EmployeeId) return false;

            // 2. HR Verify Step
            if (entity.ApprovalStatus == "PENDING_APPROVAL")
            {
                return approverRole == AppRoles.HR;
            }

            // 3. Leader Approve Step
            if (entity.ApprovalStatus == "PENDING_LEADER")
            {
                if (approverRole == AppRoles.Leader)
                {
                    // Enforce Leader scope
                    bool inScope = await context.LeaderAssignments.AsNoTracking().AnyAsync(a => a.UserId == approver.Id
                        && a.EffectiveFrom <= DateTime.Now && (a.EffectiveTo == null || a.EffectiveTo > DateTime.Now)
                        && a.OrganizationalUnit == entity.DepartmentSnapshot);
                    return inScope;
                }
                return false;
            }

            return false;
        }

        public async Task OnApprovedAsync(RewardRecord entity, ApplicationDbContext context)
        {
            var originalStatus = context.Entry(entity).Property(e => e.ApprovalStatus).OriginalValue?.ToString();

            if (originalStatus == "PENDING_APPROVAL")
            {
                // It was HR verifying. Change the status to PENDING_LEADER instead of APPROVED
                entity.Status = "PENDING_LEADER";
                
            }
            else if (originalStatus == "PENDING_LEADER")
            {
                // Final approval by Leader. Keep status as APPROVED and generate PDF
                if (string.IsNullOrEmpty(entity.DecisionNumber))
                {
                    entity.DecisionNumber = $"QDKT-{entity.Id:D4}/{DateTime.Now.Year}";
                }

                try 
                {
                    string htmlContent = GenerateHtmlForPdf(entity);
                    var converter = new SelectPdf.HtmlToPdf();
                    converter.Options.PdfPageSize = SelectPdf.PdfPageSize.A4;
                    converter.Options.PdfPageOrientation = SelectPdf.PdfPageOrientation.Portrait;
                    converter.Options.MarginLeft = 20;
                    converter.Options.MarginRight = 20;
                    converter.Options.MarginTop = 20;
                    converter.Options.MarginBottom = 20;

                    var doc = converter.ConvertHtmlString(htmlContent);
                    using var ms = new System.IO.MemoryStream();
                    doc.Save(ms);
                    doc.Close();

                    var (canonicalId, _) = await _fileService.SaveProtectedFileFromBytesAsync("rewards", entity.Id.ToString(), ms.ToArray(), ".pdf");
                    entity.DecisionDocumentUrl = canonicalId;
                }
                catch (Exception ex)
                {
                    throw new Exception("Lỗi khi tạo PDF quyết định khen thưởng: " + ex.Message);
                }

            }
        }

        public Task OnRejectedAsync(RewardRecord entity, ApplicationDbContext context)
        {
            return Task.CompletedTask;
        }

        private string GenerateHtmlForPdf(RewardRecord reward)
        {
            string html = $@"
                <html>
                <head>
                    <meta charset='utf-8'>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; padding: 20px; }}
                        .text-center {{ text-align: center; }}
                        .fw-bold {{ font-weight: bold; }}
                        .mt-4 {{ margin-top: 1.5rem; }}
                    </style>
                </head>
                <body>
                    <h2 class='text-center fw-bold'>QUYẾT ĐỊNH KHEN THƯỞNG</h2>
                    <p class='text-center'>Số: {reward.DecisionNumber}</p>
                    <p class='text-center'><i>Ngày ban hành: {reward.ApprovedAt:dd/MM/yyyy}</i></p>
                    <hr/>
                    <div style='margin-top: 30px;'>
                        <p><strong>Căn cứ:</strong> Nội quy và quy chế khen thưởng của công ty.</p>
                        <p><strong>QUYẾT ĐỊNH:</strong></p>
                        <p><strong>Điều 1.</strong> Khen thưởng đối với nhân viên:</p>
                        <ul>
                            <li><strong>Họ và tên:</strong> {reward.EmployeeNameSnapshot}</li>
                            <li><strong>Mã NV:</strong> {reward.EmployeeCodeSnapshot}</li>
                            <li><strong>Phòng ban:</strong> {reward.DepartmentSnapshot}</li>
                            <li><strong>Chức vụ:</strong> {reward.PositionSnapshot}</li>
                        </ul>
                        <p><strong>Lý do khen thưởng:</strong> {reward.Achievement}</p>
                        <p><strong>Nội dung:</strong> {reward.Description}</p>
                        {(reward.HasMonetaryReward ? $"<p><strong>Mức thưởng:</strong> {reward.RewardAmount:N0} VNĐ (Kỳ lương: {reward.PayrollPeriod})</p>" : "")}
                        
                        <p><strong>Điều 2.</strong> Các phòng ban liên quan và cá nhân có tên tại Điều 1 chịu trách nhiệm thi hành quyết định này.</p>
                    </div>
                    <table style='width: 100%; margin-top: 50px;'>
                        <tr>
                            <td style='width: 50%; text-align: center;'>
                            </td>
                            <td style='width: 50%; text-align: center;'>
                                <strong>GIÁM ĐỐC / ĐẠI DIỆN LÃNH ĐẠO</strong><br/>
                                (Đã ký điện tử)<br/><br/><br/>
                                <i>{reward.ApprovedBy}</i>
                            </td>
                        </tr>
                    </table>
                </body>
                </html>";
            return html;
        }
    }
}
