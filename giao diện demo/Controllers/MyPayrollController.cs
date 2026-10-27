using giao_dien_demo.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Helpers;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace giao_dien_demo.Controllers
{
    [Authorize(Roles = $"{AppRoles.HR},{AppRoles.Leader},{AppRoles.Manager},{AppRoles.Employee}")]
    public class MyPayrollController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.IProtectedFileService _fileService;

        public MyPayrollController(ApplicationDbContext context, giao_dien_demo.Services.IProtectedFileService fileService)
        {
            _context = context;
            _fileService = fileService;
        }

        private Employee? GetCurrentEmployee()
        {
            var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(username)) return null;
            var employeeId = _context.Users.AsNoTracking()
                .Where(u => u.Username == username && u.CanLogin)
                .Select(u => u.EmployeeId)
                .SingleOrDefault();
            return employeeId.HasValue ? _context.Employees.Find(employeeId.Value) : null;
        }

        public async Task<IActionResult> Index(string period)
        {
            var emp = GetCurrentEmployee();
            if (emp == null) return RedirectToAction("Login", "Account");

            // 1. Lấy danh sách các kỳ lương có thanh toán của nhân viên này
            var allPayments = await _context.PayrollPayments
                .Where(p => p.EmployeeId == emp.Id)
                .OrderByDescending(p => p.CycleStartDate)
                .ToListAsync();

            var availablePeriods = allPayments.Select(p => p.PayrollPeriod).Distinct().ToList();
            
            ViewBag.AvailablePeriods = availablePeriods;

            if (availablePeriods.Count == 0)
            {
                // Chưa có bảng lương nào
                return View("Empty");
            }

            var selectedPeriod = string.IsNullOrEmpty(period) ? availablePeriods.First() : period;
            
            // Security: Kiểm tra kỳ lương có thật sự thuộc về NV không
            var paymentForPeriod = allPayments.FirstOrDefault(p => p.PayrollPeriod == selectedPeriod);
            if (paymentForPeriod == null)
            {
                return Unauthorized();
            }

            ViewBag.SelectedPeriod = selectedPeriod;
            ViewBag.Payment = paymentForPeriod;

            // 2. Lấy Snapshot từ Salaries table
            var cycleDate = paymentForPeriod.CycleStartDate;
            var snapshot = await _context.Salaries
                .FirstOrDefaultAsync(s => s.EmployeeId == emp.Id && s.SalaryMonth == cycleDate);

            // 3. Nếu chưa có Snapshot thì báo lỗi không có dữ liệu
            if (snapshot == null)
            {
                return View("Empty");
            }

            ViewBag.Snapshot = snapshot;

            // 4. Trend Data (6 tháng gần nhất)
            var trendData = allPayments.Take(6).OrderBy(p => p.CycleStartDate).ToList();
            ViewBag.TrendData = trendData;

            // 5. Lịch sử
            ViewBag.HistoryData = allPayments;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetDetail(string period)
        {
            var emp = GetCurrentEmployee();
            if (emp == null) return Unauthorized();

            var payment = await _context.PayrollPayments
                .FirstOrDefaultAsync(p => p.EmployeeId == emp.Id && p.PayrollPeriod == period);
                
            if (payment == null) return Unauthorized();

            var cycleDate = payment.CycleStartDate;
            var snapshot = await _context.Salaries
                .FirstOrDefaultAsync(s => s.EmployeeId == emp.Id && s.SalaryMonth == cycleDate);

            if (snapshot == null)
            {
                return NotFound("Không tìm thấy dữ liệu lương (Snapshot) cho kỳ này.");
            }

            return Json(new { success = true, payment = payment, snapshot = snapshot });
        }

        [HttpGet]
        public async Task<IActionResult> DownloadPayslip(string period)
        {
            var emp = GetCurrentEmployee();
            if (emp == null) return Unauthorized();

            var payment = await _context.PayrollPayments
                .FirstOrDefaultAsync(p => p.EmployeeId == emp.Id && p.PayrollPeriod == period);
                
            if (payment == null) return Unauthorized();

            var cycleDate = payment.CycleStartDate;
            var snapshot = await _context.Salaries
                .FirstOrDefaultAsync(s => s.EmployeeId == emp.Id && s.SalaryMonth == cycleDate);

            if (snapshot == null) return NotFound("Chưa có bảng tính lương chính thức cho kỳ này.");

            string htmlContent = $@"
                <html>
                <head>
                    <meta charset='utf-8'>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .text-center {{ text-align: center; }}
                        .fw-bold {{ font-weight: bold; }}
                        table {{ width: 100%; border-collapse: collapse; margin-top: 20px; }}
                        th, td {{ border: 1px solid #ddd; padding: 8px; }}
                        th {{ background-color: #f2f2f2; text-align: left; }}
                    </style>
                </head>
                <body>
                    <h2 class='text-center fw-bold'>PHIẾU LƯƠNG NHÂN VIÊN</h2>
                    <h3 class='text-center'>Kỳ lương: {period}</h3>
                    <p><strong>Họ và tên:</strong> {emp.Name}</p>
                    <p><strong>Mã nhân viên:</strong> {emp.EmployeeCode}</p>
                    <p><strong>Phòng ban:</strong> {emp.Department}</p>
                    <hr/>
                    <table>
                        <tr><th>Khoản mục</th><th>Số tiền (VNĐ)</th></tr>
                        <tr><td>Lương cơ bản</td><td>{snapshot.BasicSalary:N0}</td></tr>
                        <tr><td>Giờ công</td><td>{snapshot.HoursWorked:N2} giờ</td></tr>
                        <tr><td>Phụ cấp & Thưởng OT</td><td>{snapshot.BonusAndAllowance:N0}</td></tr>
                        <tr><td>Thưởng khác</td><td>{snapshot.Reward:N0}</td></tr>
                        <tr><td><strong>Tổng thu nhập (Gross)</strong></td><td><strong>{snapshot.TotalSalary:N0}</strong></td></tr>
                        <tr><td>Bảo hiểm (10.5%)</td><td>{snapshot.Insurance:N0}</td></tr>
                        <tr><td>Thuế TNCN</td><td>{snapshot.TaxTNCN:N0}</td></tr>
                        <tr><td><strong>Thực nhận (Net)</strong></td><td><strong>{snapshot.NetSalary:N0}</strong></td></tr>
                    </table>
                </body>
                </html>";

            try 
            {
                var converter = new SelectPdf.HtmlToPdf();
                converter.Options.PdfPageSize = SelectPdf.PdfPageSize.A4;
                converter.Options.PdfPageOrientation = SelectPdf.PdfPageOrientation.Portrait;
                converter.Options.MarginLeft = 20;
                converter.Options.MarginRight = 20;

                var doc = converter.ConvertHtmlString(htmlContent);
                using var ms = new System.IO.MemoryStream();
                doc.Save(ms);
                doc.Close();

                // Lưu PDF thông qua IProtectedFileService
                var (canonicalId, _) = await _fileService.SaveProtectedFileFromBytesAsync("payslips", $"{emp.Id}_{period.Replace("/", "")}", ms.ToArray(), ".pdf");
                
                // Vì không lưu CanonicalId vào DB cho Payslip (chỉ gen on-the-fly rồi lưu cache), ta trả về file luôn
                var (physicalPath, contentType, originalName) = await _fileService.GetProtectedFileAsync(canonicalId);
                var fileBytes = await System.IO.File.ReadAllBytesAsync(physicalPath);
                
                return File(fileBytes, contentType, $"Payslip_{period.Replace("/", "")}_{emp.EmployeeCode}.pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Lỗi tạo PDF: " + ex.Message);
            }
        }
    }
}

