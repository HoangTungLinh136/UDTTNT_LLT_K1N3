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

        private async Task<Employee?> GetCurrentEmployeeAsync()
        {
            var user = await CurrentActor.ResolveUserAsync(User, _context);
            if (user?.EmployeeId != null)
            {
                return await _context.Employees.FindAsync(user.EmployeeId.Value);
            }

            var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(username)) return null;

            var employeeId = await _context.Users.AsNoTracking()
                .Where(u => u.Username == username && u.CanLogin)
                .Select(u => u.EmployeeId)
                .SingleOrDefaultAsync();

            return employeeId.HasValue ? await _context.Employees.FindAsync(employeeId.Value) : null;
        }

        private static string NormalizePeriod(string? period, DateTime defaultDate)
        {
            if (string.IsNullOrWhiteSpace(period))
            {
                return $"{defaultDate.Month:D2}/{defaultDate.Year}";
            }

            var clean = period.Trim();
            if (clean.StartsWith("T", StringComparison.OrdinalIgnoreCase) || clean.StartsWith("t", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(1);
            }

            return clean;
        }

        private static int? ExtractYearFromPeriod(string? period)
        {
            if (string.IsNullOrWhiteSpace(period)) return null;
            var parts = period.Trim().Split(new[] { '/', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && int.TryParse(parts[1], out int year))
            {
                return year;
            }
            if (parts.Length == 1 && parts[0].Length == 4 && int.TryParse(parts[0], out int y))
            {
                return y;
            }
            return null;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? year, string? period, string? status, int page = 1)
        {
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return RedirectToAction("Login", "Account");

            var model = new EmployeePayrollViewModel
            {
                EmployeeCode = emp.EmployeeCode,
                EmployeeName = emp.Name,
                Department = emp.Department,
                SelectedYear = year,
                SelectedPeriod = period,
                SelectedStatus = status,
                CurrentPage = page < 1 ? 1 : page,
                PageSize = 5
            };

            // 1. LẤY TẤT CẢ DỮ LIỆU CỦA RIÊNG EMPLOYEE ĐĂNG NHẬP
            var allPayments = await _context.PayrollPayments
                .Where(p => p.EmployeeId == emp.Id)
                .OrderByDescending(p => p.CycleStartDate)
                .ThenByDescending(p => p.Id)
                .ToListAsync();

            var allSnapshots = await _context.Salaries
                .Where(s => s.EmployeeId == emp.Id)
                .OrderByDescending(s => s.SalaryMonth)
                .ToListAsync();

            if (!allPayments.Any() && !allSnapshots.Any())
            {
                model.HasData = false;
                return View(model);
            }

            // 2. TẬP HỢP DANH SÁCH CÁC KỲ LƯƠNG ĐÃ ĐƯỢC CHUẨN HÓA
            var periodDict = new Dictionary<string, (PayrollPayment? Payment, Salary? Snapshot)>(StringComparer.OrdinalIgnoreCase);

            foreach (var payment in allPayments)
            {
                var periodKey = NormalizePeriod(payment.PayrollPeriod, payment.CycleStartDate > DateTime.MinValue ? payment.CycleStartDate : payment.CreatedAt);
                if (!periodDict.ContainsKey(periodKey))
                {
                    periodDict[periodKey] = (payment, null);
                }
                else
                {
                    var existing = periodDict[periodKey];
                    periodDict[periodKey] = (existing.Payment ?? payment, existing.Snapshot);
                }
            }

            foreach (var snap in allSnapshots)
            {
                var periodKey = $"{snap.SalaryMonth.Month:D2}/{snap.SalaryMonth.Year}";
                if (periodDict.ContainsKey(periodKey))
                {
                    var existing = periodDict[periodKey];
                    periodDict[periodKey] = (existing.Payment, snap);
                }
                else
                {
                    periodDict[periodKey] = (null, snap);
                }
            }

            // Danh sách các năm và kỳ lương phục vụ bộ lọc
            var allPeriodKeys = periodDict.Keys.OrderByDescending(k => {
                var p = k.Split('/');
                if (p.Length == 2 && int.TryParse(p[1], out int y) && int.TryParse(p[0], out int m))
                    return y * 100 + m;
                return 0;
            }).ToList();

            model.AvailablePeriods = allPeriodKeys;
            model.AvailableYears = allPeriodKeys
                .Select(ExtractYearFromPeriod)
                .Where(y => y.HasValue)
                .Select(y => y!.Value)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();

            if (!model.AvailableYears.Any())
            {
                model.AvailableYears.Add(DateTime.Now.Year);
            }

            // 3. TẠO DANH SÁCH CHI TIẾT CÁC BẢN GHI LƯƠNG
            var fullList = new List<EmployeePayrollItemDto>();
            int idx = 1;

            foreach (var key in allPeriodKeys)
            {
                var entry = periodDict[key];
                var payment = entry.Payment;
                var snapshot = entry.Snapshot;

                double gross = snapshot?.TotalSalary ?? (payment != null ? payment.NetSalary * 1.25 : (double)emp.Salary);
                double net = payment?.NetSalary ?? snapshot?.NetSalary ?? (double)emp.Salary;
                double deduction = snapshot != null ? (snapshot.Insurance + snapshot.TaxTNCN + snapshot.Discipline) : Math.Max(0, gross - net);
                if (deduction == 0 && gross > net) deduction = gross - net;

                double actualWorkDays = payment?.WorkDays > 0 ? payment.WorkDays : (snapshot?.HoursWorked > 0 ? Math.Round(snapshot.HoursWorked / 8.0, 0) : 22);
                if (actualWorkDays <= 0) actualWorkDays = 22;

                PaymentStatus payStatus = payment?.Status ?? PaymentStatus.DaThanhToan;
                string statusText = payStatus == PaymentStatus.DaThanhToan ? "Đã thanh toán" : (payStatus == PaymentStatus.ChoThanhToan ? "Chờ thanh toán" : "Chưa thanh toán");

                var item = new EmployeePayrollItemDto
                {
                    Id = payment?.Id ?? (snapshot?.Id ?? idx),
                    Index = idx++,
                    Period = key,
                    GrossSalary = gross,
                    TotalDeduction = deduction,
                    NetSalary = net,
                    ActualWorkDays = actualWorkDays,
                    StandardWorkDays = 22,
                    Status = payStatus,
                    StatusText = statusText,
                    PaymentDate = payment?.PaymentDate,
                    PaidAt = payment?.PaidAt,
                    CycleStartDate = payment?.CycleStartDate ?? (snapshot?.SalaryMonth ?? DateTime.MinValue),
                    CycleEndDate = payment?.CycleEndDate ?? (snapshot?.SalaryMonth.AddMonths(1).AddDays(-1) ?? DateTime.MinValue)
                };

                fullList.Add(item);
            }

            // 4. ÁP DỤNG BỘ LỌC
            var filtered = fullList.AsEnumerable();

            if (year.HasValue)
            {
                filtered = filtered.Where(x => ExtractYearFromPeriod(x.Period) == year.Value);
            }

            if (!string.IsNullOrWhiteSpace(period) && period != "Tất cả")
            {
                filtered = filtered.Where(x => string.Equals(x.Period, period.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                              string.Equals(x.Period, "T" + period.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "Tất cả")
            {
                filtered = filtered.Where(x => string.Equals(x.StatusText, status.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            var filteredList = filtered.ToList();
            model.TotalItems = filteredList.Count;

            // Đánh lại số thứ tự sau khi lọc
            for (int i = 0; i < filteredList.Count; i++)
            {
                filteredList[i].Index = i + 1;
            }

            // 5. PHÂN TRANG (5 bản ghi / trang)
            model.PayrollList = filteredList
                .Skip((model.CurrentPage - 1) * model.PageSize)
                .Take(model.PageSize)
                .ToList();

            // 6. XÁC ĐỊNH KỲ LƯƠNG ĐANG CHỌN ĐỂ HIỂN THỊ TRÊN 4 CARD VÀ BIỂU ĐỒ
            EmployeePayrollItemDto? selectedItem = null;
            if (!string.IsNullOrWhiteSpace(period) && period != "Tất cả")
            {
                selectedItem = fullList.FirstOrDefault(x => string.Equals(x.Period, period.Trim(), StringComparison.OrdinalIgnoreCase));
            }
            selectedItem ??= fullList.FirstOrDefault();

            if (selectedItem != null)
            {
                model.Summary = new PayrollSummaryCardDto
                {
                    PeriodLabel = $"Tháng {selectedItem.Period}",
                    NetSalary = selectedItem.NetSalary,
                    GrossSalary = selectedItem.GrossSalary,
                    TotalDeduction = selectedItem.TotalDeduction,
                    ActualWorkDays = selectedItem.ActualWorkDays,
                    StandardWorkDays = selectedItem.StandardWorkDays
                };

                // Phân tích cơ cấu thu nhập cho kỳ đang chọn
                var snapshot = periodDict.ContainsKey(selectedItem.Period) ? periodDict[selectedItem.Period].Snapshot : null;

                double basic = snapshot?.BasicSalary > 0 ? snapshot.BasicSalary : (double)emp.Salary;
                double allowances = snapshot?.BonusAndAllowance > 0 ? snapshot.BonusAndAllowance : ((double)emp.PositionAllowance + (double)emp.HazardousAllowance + (double)emp.QualificationAllowance);
                double bonus = snapshot?.Reward > 0 ? snapshot.Reward : (snapshot != null ? (snapshot.HolidayBonus + snapshot.DiligenceBonus) : 0);
                double totalGross = selectedItem.GrossSalary;
                
                // Đảm bảo tổng các khoản bằng Gross
                double currentSum = basic + allowances + bonus;
                double other = Math.Max(0, totalGross - currentSum);

                // Nếu chưa có breakdown cụ thể, chia theo tỷ lệ hợp lý thực tế
                if (totalGross > 0 && currentSum == 0)
                {
                    basic = Math.Round(totalGross * 0.633, 0);
                    allowances = Math.Round(totalGross * 0.190, 0);
                    bonus = Math.Round(totalGross * 0.114, 0);
                    other = Math.Max(0, totalGross - (basic + allowances + bonus));
                }

                model.IncomeStructure = new IncomeStructureDto
                {
                    PeriodLabel = selectedItem.Period,
                    BasicSalary = basic,
                    Allowances = allowances,
                    Bonus = bonus,
                    Other = other,
                    TotalGross = totalGross
                };
            }

            // 7. LỊCH SỬ NHẬN LƯƠNG (TIMELINE DỌC)
            model.PaymentHistory = fullList
                .Where(x => x.Status == PaymentStatus.DaThanhToan || x.PaidAt.HasValue)
                .OrderByDescending(x => x.PaidAt ?? (x.PaymentDate ?? DateTime.MinValue))
                .ThenByDescending(x => x.Index)
                .Take(6)
                .Select(x => new PaymentHistoryItemDto
                {
                    Id = x.Id,
                    Period = x.Period,
                    NetSalary = x.NetSalary,
                    PaymentDate = x.PaymentDate,
                    PaidAt = x.PaidAt ?? x.PaymentDate,
                    Status = x.Status
                })
                .ToList();

            return View(model);
        }

        // =========================================================================
        // AJAX API LẤY CHI TIẾT BẢNG LƯƠNG ĐỂ HIỂN THỊ MODAL
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> GetPayrollDetail(string period)
        {
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Unauthorized(new { message = "Chưa đăng nhập." });

            if (string.IsNullOrWhiteSpace(period))
            {
                return BadRequest(new { message = "Kỳ lương không hợp lệ." });
            }

            var cleanPeriod = NormalizePeriod(period, DateTime.Now);

            int targetMonth = 0;
            int targetYear = 0;
            var parts = cleanPeriod.Split(new[] { '/', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && int.TryParse(parts[0], out int m) && int.TryParse(parts[1], out int y))
            {
                targetMonth = m;
                targetYear = y;
            }

            // Security: Chỉ lấy thông tin của chính Employee đăng nhập
            var payment = await _context.PayrollPayments
                .FirstOrDefaultAsync(p => p.EmployeeId == emp.Id && (p.PayrollPeriod == cleanPeriod || p.PayrollPeriod == "T" + cleanPeriod || p.PayrollPeriod == period));

            var snapshot = targetMonth > 0 && targetYear > 0
                ? await _context.Salaries.FirstOrDefaultAsync(s => s.EmployeeId == emp.Id && s.SalaryMonth.Month == targetMonth && s.SalaryMonth.Year == targetYear)
                : null;

            if (payment == null && snapshot == null)
            {
                return NotFound(new { message = "Không tìm thấy dữ liệu bảng lương cho kỳ này." });
            }

            double gross = snapshot?.TotalSalary ?? (payment != null ? payment.NetSalary * 1.25 : (double)emp.Salary);
            double net = payment?.NetSalary ?? (snapshot?.NetSalary ?? (double)emp.Salary);
            double deduction = snapshot != null ? (snapshot.Insurance + snapshot.TaxTNCN + snapshot.Discipline) : Math.Max(0, gross - net);

            double actualDays = payment?.WorkDays > 0 ? payment.WorkDays : (snapshot?.HoursWorked > 0 ? Math.Round(snapshot.HoursWorked / 8.0, 0) : 22);

            var detail = new EmployeePayrollDetailDto
            {
                Id = payment?.Id ?? (snapshot?.Id ?? 0),
                Period = cleanPeriod,
                EmployeeCode = emp.EmployeeCode ?? "",
                EmployeeName = emp.Name ?? "",
                Department = emp.Department ?? "",
                BankName = payment?.BankName ?? emp.BankName,
                BankAccountNumber = payment?.BankAccountNumber ?? emp.BankAccountNumber,
                ActualWorkDays = actualDays,
                StandardWorkDays = 22,
                HoursWorked = snapshot?.HoursWorked > 0 ? snapshot.HoursWorked : actualDays * 8.0,

                // Thu nhập
                BasicSalary = snapshot?.BasicSalary > 0 ? snapshot.BasicSalary : (double)emp.Salary,
                PositionAllowance = (double)emp.PositionAllowance,
                OtherAllowances = snapshot?.BonusAndAllowance > 0 ? Math.Max(0, snapshot.BonusAndAllowance - (double)emp.PositionAllowance) : ((double)emp.HazardousAllowance + (double)emp.QualificationAllowance),
                TotalAllowances = snapshot?.BonusAndAllowance > 0 ? snapshot.BonusAndAllowance : ((double)emp.PositionAllowance + (double)emp.HazardousAllowance + (double)emp.QualificationAllowance),
                PerformanceBonus = snapshot?.Reward > 0 ? snapshot.Reward : 0,
                OtherBonus = snapshot != null ? (snapshot.HolidayBonus + snapshot.DiligenceBonus) : 0,
                TotalBonus = snapshot != null ? (snapshot.Reward + snapshot.HolidayBonus + snapshot.DiligenceBonus) : 0,
                OtherIncome = Math.Max(0, gross - ((snapshot?.BasicSalary > 0 ? snapshot.BasicSalary : (double)emp.Salary) + (snapshot?.BonusAndAllowance ?? 0))),
                TotalGrossSalary = gross,

                // Khấu trừ
                SocialInsurance = snapshot?.Insurance > 0 ? Math.Round(snapshot.Insurance * (8.0 / 10.5), 0) : Math.Round(gross * 0.08, 0),
                HealthInsurance = snapshot?.Insurance > 0 ? Math.Round(snapshot.Insurance * (1.5 / 10.5), 0) : Math.Round(gross * 0.015, 0),
                UnemploymentInsurance = snapshot?.Insurance > 0 ? Math.Round(snapshot.Insurance * (1.0 / 10.5), 0) : Math.Round(gross * 0.01, 0),
                TotalInsurance = snapshot?.Insurance > 0 ? snapshot.Insurance : Math.Round(gross * 0.105, 0),
                PersonalIncomeTax = snapshot?.TaxTNCN > 0 ? snapshot.TaxTNCN : Math.Max(0, deduction - (snapshot?.Insurance ?? (gross * 0.105))),
                OtherDeductions = snapshot?.Discipline ?? 0,
                TotalDeductions = deduction,

                // Thực lĩnh
                NetSalary = net,
                Status = payment?.Status ?? PaymentStatus.DaThanhToan,
                StatusText = (payment?.Status ?? PaymentStatus.DaThanhToan) == PaymentStatus.DaThanhToan ? "Đã thanh toán" : "Chờ thanh toán",
                PaymentDate = payment?.PaymentDate,
                PaidAt = payment?.PaidAt,
                PaymentNote = payment?.PaymentNote
            };

            return Json(new { success = true, data = detail });
        }

        // =========================================================================
        // TẢI PHIẾU LƯƠNG PDF
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> DownloadPayslip(string period)
        {
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Unauthorized();

            var cleanPeriod = NormalizePeriod(period, DateTime.Now);

            var payment = await _context.PayrollPayments
                .FirstOrDefaultAsync(p => p.EmployeeId == emp.Id && (p.PayrollPeriod == cleanPeriod || p.PayrollPeriod == "T" + cleanPeriod || p.PayrollPeriod == period));
                
            int dlMonth = 0;
            int dlYear = 0;
            var dlParts = cleanPeriod.Split(new[] { '/', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (dlParts.Length >= 2 && int.TryParse(dlParts[0], out int dm) && int.TryParse(dlParts[1], out int dy))
            {
                dlMonth = dm;
                dlYear = dy;
            }

            var snapshot = dlMonth > 0 && dlYear > 0
                ? await _context.Salaries.FirstOrDefaultAsync(s => s.EmployeeId == emp.Id && s.SalaryMonth.Month == dlMonth && s.SalaryMonth.Year == dlYear)
                : null;

            double basic = snapshot?.BasicSalary > 0 ? snapshot.BasicSalary : (double)emp.Salary;
            double gross = snapshot?.TotalSalary ?? (payment?.NetSalary != null ? payment.NetSalary * 1.25 : basic);
            double net = payment?.NetSalary ?? (snapshot?.NetSalary ?? basic);
            double deduction = snapshot != null ? (snapshot.Insurance + snapshot.TaxTNCN) : Math.Max(0, gross - net);

            string htmlContent = $@"
                <html>
                <head>
                    <meta charset='utf-8'>
                    <style>
                        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #1e293b; padding: 25px; }}
                        .text-center {{ text-align: center; }}
                        .fw-bold {{ font-weight: bold; }}
                        .header-box {{ border-bottom: 2px solid #2563eb; padding-bottom: 15px; margin-bottom: 20px; }}
                        .title {{ font-size: 22px; color: #1e3a8a; margin: 0; }}
                        .subtitle {{ font-size: 14px; color: #64748b; margin-top: 5px; }}
                        .info-grid {{ display: table; width: 100%; margin-bottom: 20px; }}
                        .info-row {{ display: table-row; }}
                        .info-cell {{ display: table-cell; padding: 4px 8px; font-size: 14px; }}
                        table {{ width: 100%; border-collapse: collapse; margin-top: 15px; }}
                        th, td {{ border: 1px solid #cbd5e1; padding: 10px; font-size: 14px; }}
                        th {{ background-color: #f8fafc; text-align: left; color: #334155; }}
                        .text-end {{ text-align: right; }}
                        .bg-light {{ background-color: #f1f5f9; }}
                        .total-row {{ font-weight: bold; background-color: #eff6ff; color: #1d4ed8; }}
                    </style>
                </head>
                <body>
                    <div class='header-box text-center'>
                        <h2 class='title fw-bold'>PHIẾU LƯƠNG NHÂN VIÊN</h2>
                        <div class='subtitle'>Kỳ lương: {cleanPeriod}</div>
                    </div>
                    <div class='info-grid'>
                        <div class='info-row'>
                            <div class='info-cell'><strong>Họ và tên:</strong> {emp.Name}</div>
                            <div class='info-cell'><strong>Mã nhân viên:</strong> {emp.EmployeeCode}</div>
                        </div>
                        <div class='info-row'>
                            <div class='info-cell'><strong>Phòng ban:</strong> {emp.Department}</div>
                            <div class='info-cell'><strong>Tài khoản nhận:</strong> {emp.BankAccountNumber} ({emp.BankName})</div>
                        </div>
                    </div>
                    <table>
                        <thead>
                            <tr>
                                <th>Khoản mục</th>
                                <th class='text-end'>Số tiền (VNĐ)</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr>
                                <td>1. Lương cơ bản</td>
                                <td class='text-end'>{basic:N0} đ</td>
                            </tr>
                            <tr>
                                <td>2. Phụ cấp</td>
                                <td class='text-end'>{(snapshot?.BonusAndAllowance ?? 0):N0} đ</td>
                            </tr>
                            <tr>
                                <td>3. Thưởng & Thu nhập khác</td>
                                <td class='text-end'>{(snapshot?.Reward ?? 0):N0} đ</td>
                            </tr>
                            <tr class='bg-light'>
                                <td><strong>TỔNG THU NHẬP (GROSS)</strong></td>
                                <td class='text-end fw-bold'>{gross:N0} đ</td>
                            </tr>
                            <tr>
                                <td>4. Bảo hiểm bắt buộc (10.5%)</td>
                                <td class='text-end'>-{(snapshot?.Insurance ?? (gross * 0.105)):N0} đ</td>
                            </tr>
                            <tr>
                                <td>5. Thuế TNCN</td>
                                <td class='text-end'>-{(snapshot?.TaxTNCN ?? 0):N0} đ</td>
                            </tr>
                            <tr class='bg-light'>
                                <td><strong>TỔNG KHẤU TRỪ</strong></td>
                                <td class='text-end fw-bold' style='color:#dc2626;'>-{deduction:N0} đ</td>
                            </tr>
                            <tr class='total-row'>
                                <td><strong>THỰC LĨNH (NET)</strong></td>
                                <td class='text-end fw-bold' style='font-size: 16px;'>{net:N0} đ</td>
                            </tr>
                        </tbody>
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

                var (canonicalId, _) = await _fileService.SaveProtectedFileFromBytesAsync("payslips", $"{emp.Id}_{cleanPeriod.Replace("/", "")}", ms.ToArray(), ".pdf");
                var (physicalPath, contentType, originalName) = await _fileService.GetProtectedFileAsync(canonicalId);
                var fileBytes = await System.IO.File.ReadAllBytesAsync(physicalPath);
                
                return File(fileBytes, contentType, $"PhieuLuong_{cleanPeriod.Replace("/", "_")}_{emp.EmployeeCode}.pdf");
            }
            catch (Exception)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(htmlContent);
                return File(bytes, "text/html", $"PhieuLuong_{cleanPeriod.Replace("/", "_")}_{emp.EmployeeCode}.html");
            }
        }
    }
}
