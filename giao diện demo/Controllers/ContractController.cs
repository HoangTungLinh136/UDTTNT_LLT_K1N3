using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Models;
using giao_dien_demo.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using giao_dien_demo.Hubs;
using giao_dien_demo.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using SelectPdf;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize]
    public class ContractController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<DashboardHub> _hubContext;
        private readonly IApprovalEngine _approvalEngine;
        private readonly ContractWorkflowPolicy _contractPolicy;
        private readonly IProtectedFileService _fileService;
        private readonly IInAppNotificationService _notifications;
        private readonly ILeadershipScopeService _leadershipScope;

        public ContractController(
            ApplicationDbContext context, 
            IHubContext<DashboardHub> hubContext,
            IApprovalEngine approvalEngine,
            ContractWorkflowPolicy contractPolicy,
            IProtectedFileService fileService,
            IInAppNotificationService notifications,
            ILeadershipScopeService leadershipScope)
        {
            _context = context;
            _hubContext = hubContext;
            _approvalEngine = approvalEngine;
            _contractPolicy = contractPolicy;
            _fileService = fileService;
            _notifications = notifications;
            _leadershipScope = leadershipScope;
        }

        private bool CheckLogin()
        {
            var user = HttpContext.Session.GetString("User");
            return !string.IsNullOrEmpty(user);
        }

        // --- 1. TRANG DÀNH SÁCH HỢP ĐỒNG ---
        public async Task<IActionResult> Index()
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");

            var role = CurrentActor.Role(User);
            if (role == "Leader") return RedirectToAction("Index", "LeaderContract");
            ViewBag.EmployeeList = role == "HR"
                ? await _context.Employees.OrderBy(e => e.Name).ToListAsync()
                : new List<Employee>();

            if (role == "HR" || role == "Leader")
            {
                var allContracts = await _context.Contracts.OrderByDescending(c => c.CreatedAt).ToListAsync();
                return View(allContracts);
            }
            else
            {
                var currentUser = await CurrentActor.ResolveUserAsync(User, _context);
                if (currentUser?.EmployeeId == null) return Forbid();
                var myContracts = await _context.Contracts
                    .AsNoTracking()
                    .Where(c => c.EmployeeId == currentUser.EmployeeId.Value)
                    .OrderByDescending(c => c.StartDate)
                    .ThenByDescending(c => c.CreatedAt)
                    .ToListAsync();
                
                var today = DateTime.Today;
                
                var currentContract = myContracts
                    .Where(c =>
                        c.Status == "Còn hiệu lực" &&
                        c.StartDate.Date <= today &&
                        c.EndDate.Date >= today)
                    .OrderByDescending(c => c.StartDate)
                    .ThenByDescending(c => c.CreatedAt)
                    .FirstOrDefault();
                
                ViewBag.CurrentContractId = currentContract?.Id;
                
                Response.Headers["Cache-Control"] =
                    "private, no-store, no-cache, max-age=0";
                Response.Headers["Pragma"] = "no-cache";

                return View("MyContracts", myContracts);
            }
        }

        // --- 2. TẠO HỢP ĐỒNG MỚI (HR) ---
        [HttpGet]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> Create()
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            ViewBag.Employees = await _context.Employees.OrderBy(e => e.Name).ToListAsync();
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "HR")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Contract contract)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");

            if (contract != null)
            {
                if (!contract.EmployeeId.HasValue) return BadRequest("Nhân viên không hợp lệ.");
                var employee = await _context.Employees.FindAsync(contract.EmployeeId.Value);
                if (employee == null) return BadRequest("Nhân viên không tồn tại.");
                contract.EmployeeNameSnapshot = employee.Name;

                // Historical contract snapshot.
                // Never reconstruct old contracts from the current Employee profile.
                contract.DepartmentSnapshot = employee.Department;
                contract.PositionSnapshot = employee.Position;
                contract.JobTitleSnapshot = employee.JobTitle;

                // Base salary at contract creation time.
                // This is NOT payroll net salary.
                contract.ContractSalarySnapshot = employee.Salary;

                contract.CreatedAt = DateTime.Now;
                contract.Status = "Chờ nhân viên ký";
                if (contract.StartDate == default) contract.StartDate = DateTime.Now;
                contract.EndDate = contract.StartDate.AddYears(1);

                _context.Contracts.Add(contract);
                await _context.SaveChangesAsync();

                // P43.1: Canonical notification to the employee's linked User
                if (contract.EmployeeId.HasValue)
                {
                    var recipientUser = await _context.Users.AsNoTracking()
                        .FirstOrDefaultAsync(u => u.EmployeeId == contract.EmployeeId.Value && u.CanLogin);
                    if (recipientUser != null)
                    {
                        await _notifications.SendAsync(
                            recipientUser.Username,
                            $"HR đã gửi hợp đồng mới cho bạn.",
                            "CONTRACT",
                            $"/Contract/Index",
                            contract.Id);
                    }
                }
            }
            return RedirectToAction("Index");
        }

        // --- 3. NHÂN VIÊN KÝ XÁC NHẬN ---
        [HttpPost]
        [Authorize(Roles = "Employee")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitSignature(int Id, string Duration, string Signature, DateTime ExtensionDate)
        {
            var contract = await _context.Contracts.FindAsync(Id);
            if (contract == null) return NotFound();

            var user = await CurrentActor.ResolveUserAsync(User, _context);
            if (user?.EmployeeId == null || contract.EmployeeId != user.EmployeeId) return Forbid();
            if (contract.Status != "Chờ nhân viên ký") return Conflict("Hợp đồng không ở trạng thái chờ ký.");
            if (string.IsNullOrWhiteSpace(Duration) || string.IsNullOrWhiteSpace(Signature))
            {
                return BadRequest("Thiếu thông tin ký hợp đồng.");
            }

            contract.Duration = Duration;
            contract.Signature = Signature.Trim();
            contract.EmployeeSignedAt = DateTime.Now;
            contract.EndDate = ExtensionDate != default ? ExtensionDate : contract.StartDate.AddYears(1);
            contract.Status = "Đã ký (Chờ duyệt cuối)";

            await _context.SaveChangesAsync();

            // P43.1: Canonical notification to HR role about employee signature
            await _notifications.SendAsync(
                "HR",
                $"Nhân viên {contract.EmployeeNameSnapshot} đã ký xác nhận hợp đồng.",
                "CONTRACT",
                $"/Contract/Index",
                contract.Id);
            return RedirectToAction("Index");
        }

        // --- 4. DUYỆT HỢP ĐỒNG & TỰ ĐỘNG ĐỒNG BỘ NHÂN SỰ VÀ BÁO CÁO ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> Approve(int id)
        {
            if (!CheckLogin()) return Unauthorized();
            var role = CurrentActor.Role(User);
            var username = CurrentActor.Username(User);
            if (role != "HR") return Forbid();

            var contract = await _context.Contracts.FindAsync(id);
            if (contract == null) return NotFound();

            var (success, message) = await _approvalEngine.ProcessDecisionAsync(contract, "APPROVE", username, role, null, _contractPolicy);

            if (success)
            {
                // After approval, ContractWorkflowPolicy generates the PDF and sets PdfFilePath to a Canonical ID
                contract.Status = "Còn hiệu lực";
                _context.Contracts.Update(contract);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Duyệt thành công!" });
            }

            return StatusCode(500, message);
        }

        // --- 5. HỦY HỢP ĐỒNG ---
        [HttpPost]
        [Authorize(Roles = "HR")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var contract = await _context.Contracts.FindAsync(id);
                if (contract != null)
                {
                    await _context.Entry(contract).ReloadAsync();
                    if (contract.Status == "Còn hiệu lực")
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { success = false, message = "Không thể hủy hợp đồng đã có hiệu lực. Hãy sử dụng quy trình chấm dứt hợp đồng." });
                    }

                    if (contract.Status == "Đã hủy")
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { success = false, message = "Hợp đồng đã bị hủy trước đó." });
                    }

                    var oldStatus = contract.Status;
                    contract.Status = "Đã hủy";
                    _context.Contracts.Update(contract);

                    var currentUsername = CurrentActor.Username(User) ?? HttpContext.Session.GetString("User") ?? "System";
                    _context.AuditLogs.Add(new AuditLog
                    {
                        Timestamp = DateTime.Now,
                        UserId = currentUsername,
                        Username = currentUsername,
                        Role = "HR",
                        Module = "Contract",
                        Action = "Cancel",
                        Resource = $"Contract_{id}",
                        Status = "Success",
                        NewValues = $"Status={oldStatus}->Đã hủy"
                    });

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return Ok(new { success = true });
                }
                return NotFound();
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                return BadRequest(new { success = false, message = "Dữ liệu đã bị thay đổi bởi người khác. Vui lòng tải lại trang." });
            }
            catch (Exception)
            {
                return BadRequest(new { success = false, message = "Đã xảy ra lỗi trong quá trình hủy hợp đồng." });
            }
        }

        // --- 6. XÓA HỢP ĐỒNG ---
        [HttpPost]
        [Authorize(Roles = "HR")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            return await Cancel(id); // Dùng chung logic với Cancel cho gọn, thực hiện soft-cancel
        }

        // --- API: Lấy chi tiết hợp đồng của tôi (Bảo mật IDOR) ---
        [HttpGet]
        public async Task<IActionResult> GetContractDetail(int id)
        {
            if (!CheckLogin()) return Unauthorized();

            var role = CurrentActor.Role(User);
            
            var contract = await _context.Contracts.FindAsync(id);
            if (contract == null) return NotFound("Không tìm thấy hợp đồng");

            if (role == "Leader" && (contract.EmployeeId == null || !await _context.Employees.AnyAsync(e => e.Id == contract.EmployeeId && _leadershipScope.GetOverseenOrganizationalUnits().Contains(e.Department))))
                return Forbid();
            if (role != "HR" && role != "Leader")
            {
                var currentUser = await CurrentActor.ResolveUserAsync(User, _context);
                if (currentUser?.EmployeeId == null || contract.EmployeeId != currentUser.EmployeeId.Value) return Forbid();
            }
            
            Response.Headers["Cache-Control"] = "private, no-store, no-cache, max-age=0";
                        Response.Headers["Pragma"] = "no-cache";
            
                        return Json(new
                        {
                            id = contract.Id,
                            contractCode = $"HD-{contract.Id:D4}",
                            duration = contract.Duration,
                            startDate = contract.StartDate,
                            endDate = contract.EndDate,
                            status = contract.Status,
                            content = contract.Content,

                signedDate = contract.EmployeeSignedAt,
                effectiveDate = contract.StartDate,

                department = contract.DepartmentSnapshot,
                position = contract.PositionSnapshot,
                jobTitle = contract.JobTitleSnapshot,

                contractSalary = contract.ContractSalarySnapshot,
                currency = "VND",

                historicalSnapshotAvailable =
                    !string.IsNullOrWhiteSpace(contract.DepartmentSnapshot) ||
                    !string.IsNullOrWhiteSpace(contract.PositionSnapshot) ||
                    !string.IsNullOrWhiteSpace(contract.JobTitleSnapshot) ||
                    contract.ContractSalarySnapshot.HasValue,
                            createdAt = contract.CreatedAt,
                            employeeSigned = !string.IsNullOrWhiteSpace(contract.Signature),
                            hrSigned = !string.IsNullOrWhiteSpace(contract.HrSignature),
                            hasPdf = !string.IsNullOrWhiteSpace(contract.PdfFilePath)
                        });
        }

        // --- API: Tải PDF hợp đồng (Bảo mật IDOR) ---
        [HttpGet]
        public async Task<IActionResult> DownloadPdf(int id)
        {
            if (!CheckLogin()) return Unauthorized();

            Response.Headers["Cache-Control"] = "private, no-store, no-cache, max-age=0";
            Response.Headers["Pragma"] = "no-cache";

            var role = CurrentActor.Role(User);
            
            var contract = await _context.Contracts.FindAsync(id);
            if (contract == null) return NotFound("Không tìm thấy hợp đồng");

            if (role == "Leader" && (contract.EmployeeId == null || !await _context.Employees.AnyAsync(e => e.Id == contract.EmployeeId && _leadershipScope.GetOverseenOrganizationalUnits().Contains(e.Department))))
                return Forbid();
            if (role != "HR" && role != "Leader")
            {
                var currentUser = await CurrentActor.ResolveUserAsync(User, _context);
                if (currentUser?.EmployeeId == null || contract.EmployeeId != currentUser.EmployeeId.Value) return Forbid();
            }

            if (!string.IsNullOrEmpty(contract.PdfFilePath))
            {
                try
                {
                    var (physicalPath, contentType, originalName) = await _fileService.GetProtectedFileAsync(contract.PdfFilePath);
                    var fileBytes = await System.IO.File.ReadAllBytesAsync(physicalPath);
                    return File(fileBytes, contentType, originalName);
                }
                catch (FileNotFoundException)
                {
                    // Fallback to generating preview if file is missing
                }
                catch (UnauthorizedAccessException)
                {
                    return Forbid();
                }
            }
            
            string htmlContent = GenerateHtmlForPdf(contract);

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

            return File(ms.ToArray(), "application/pdf", $"HopDong_{contract.Id}_Preview.pdf");
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
