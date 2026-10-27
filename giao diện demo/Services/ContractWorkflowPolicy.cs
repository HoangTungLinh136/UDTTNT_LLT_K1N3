using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using Microsoft.AspNetCore.SignalR;
using giao_dien_demo.Hubs;

namespace giao_dien_demo.Services
{
    public class ContractWorkflowPolicy : IWorkflowPolicy<Contract>
    {
        public string DomainName => "Contract";
        private readonly IProtectedFileService _fileService;
        private readonly IHubContext<DashboardHub> _hubContext;

        public ContractWorkflowPolicy(IProtectedFileService fileService, IHubContext<DashboardHub> hubContext)
        {
            _fileService = fileService;
            _hubContext = hubContext;
        }

        public Task<bool> IsEligibleApproverAsync(Contract entity, string approverUsername, string approverRole, ApplicationDbContext context)
        {
            if (approverRole == "HR")
            {
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public async Task OnApprovedAsync(Contract entity, ApplicationDbContext context)
        {
            // 1. Employee verification (Link via EmployeeId, no auto-create)
            // P0-D2D: Employee.Position is an organizational role (e.g. Trưởng phòng, Kỹ sư).
            // Contract approval MUST NOT corrupt Employee.Position to "Chính thức" or any employment status/type.
            // Do NOT mutate JobTitle, Department, or Status.
            if (entity.EmployeeId.HasValue)
            {
                var emp = await context.Employees.FindAsync(entity.EmployeeId.Value);
                if (emp == null)
                {
                    throw new Exception($"Nhân viên liên kết với hợp đồng không tồn tại (EmployeeId: {entity.EmployeeId.Value}). Không thể duyệt.");
                }
            }
            else
            {
                throw new Exception("Hợp đồng này không được liên kết với nhân viên nào (Thiếu EmployeeId). Không thể duyệt.");
            }

            // 2. Generate PDF and save via ProtectedFileService
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

                var (canonicalId, _) = await _fileService.SaveProtectedFileFromBytesAsync("contracts", entity.Id.ToString(), ms.ToArray(), ".pdf");
                entity.PdfFilePath = canonicalId; // Store canonical ID instead of physical path
            }
            catch (Exception ex)
            {
                throw new Exception("Lỗi khi tạo PDF hợp đồng: " + ex.Message);
            }

            // 3. Real-time updates (SignalR)
            var totalActive = await context.Employees.CountAsync(e => e.Status == "Active" || e.Status == "Probation");
            await _hubContext.Clients.Group("role:HR").SendAsync("UpdateEmployeeCount", totalActive);
            await _hubContext.Clients.Group("role:HR").SendAsync("ContractApproved", entity.EmployeeNameSnapshot);
        }

        public Task OnRejectedAsync(Contract entity, ApplicationDbContext context)
        {
            return Task.CompletedTask;
        }

        private string GenerateHtmlForPdf(Contract contract)
        {
            string html = $@"
                <html>
                <head>
                    <meta charset='utf-8'>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .text-center {{ text-align: center; }}
                        .fw-bold {{ font-weight: bold; }}
                        .mt-4 {{ margin-top: 1.5rem; }}
                        .signature-box {{ margin-top: 50px; text-align: center; }}
                    </style>
                </head>
                <body>
                    <h2 class='text-center fw-bold'>HỢP ĐỒNG LAO ĐỘNG</h2>
                    <p><strong>Mã HĐ:</strong> HD-{contract.Id:D4}</p>
                    <p><strong>Ngày ký:</strong> {contract.CreatedAt:dd/MM/yyyy}</p>
                    <hr/>
                    <div style='margin-bottom: 30px;'>
                        {contract.Content}
                    </div>
                    <table style='width: 100%;'>
                        <tr>
                            <td style='width: 50%; text-align: center;'>
                                <strong>NGƯỜI LAO ĐỘNG</strong><br/>
                                (Ký, ghi rõ họ tên)<br/><br/><br/>
                                {(string.IsNullOrEmpty(contract.Signature) ? "" : $"<i>{contract.Signature}</i>")}
                            </td>
                            <td style='width: 50%; text-align: center;'>
                                <strong>ĐẠI DIỆN CÔNG TY</strong><br/>
                                (Ký, ghi rõ họ tên)<br/><br/><br/>
                                {(string.IsNullOrEmpty(contract.HrSignature) ? "" : $"<i>{contract.HrSignature}</i>")}
                            </td>
                        </tr>
                    </table>
                </body>
                </html>";
            return html;
        }
    }
}
