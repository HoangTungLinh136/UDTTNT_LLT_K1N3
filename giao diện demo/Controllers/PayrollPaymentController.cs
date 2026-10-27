using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using giao_dien_demo.Authorization;
using giao_dien_demo.Models;
using giao_dien_demo.Data;
using giao_dien_demo.Hubs;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using giao_dien_demo.Helpers;
using giao_dien_demo.Services;

namespace giao_dien_demo.Controllers
{
    // DTO nội bộ để trả kết quả tính lương kèm EmployeeId (Salary model không track EmployeeId)
    internal class SalaryResult
    {
        public int EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public double NetSalary { get; set; }
        public double WorkDays { get; set; }
    }
    /// <summary>
    /// Controller module Thanh Toán Lương.
    /// Chỉ đọc NetSalary từ hệ thống lương hiện tại.
    /// KHÔNG tự tính lại lương, KHÔNG thay đổi Employee.Salary.
    /// </summary>
    [Authorize(Policy = AppPolicies.HROnly)]
    [AutoValidateAntiforgeryToken]
    public class PayrollPaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<DashboardHub> _hubContext;
        private readonly giao_dien_demo.Services.IPayrollCalculationService _payrollService;
        private readonly IInAppNotificationService _notifications;

        public PayrollPaymentController(ApplicationDbContext context, IHubContext<DashboardHub> hubContext, giao_dien_demo.Services.IPayrollCalculationService payrollService, IInAppNotificationService notifications)
        {
            _context = context;
            _hubContext = hubContext;
            _payrollService = payrollService;
            _notifications = notifications;
        }

        // ================================================================
        // KIỂM TRA QUYỀN: Chỉ HR và Admin
        // ================================================================
        private bool CheckHrOrAdmin()
        {
            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            return role == "HR";
        }

        private bool CheckLogin()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("User"));
        }

        // ================================================================
        // TÍNH LƯƠNG: Sử dụng canonical IPayrollCalculationService
        // ================================================================
        private async Task<List<Salary>> LayDanhSachLuongHienTai(string? period)
        {
            var cycle = PayrollCycleHelper.GetCycleByPeriod(period);
            var result = await _payrollService.CalculatePayrollAsync(cycle.StartDate);
            return result;
        }

        // ================================================================
        // 1. DÀNH SÁCH THANH TOÁN
        // ================================================================
        public async Task<IActionResult> Index(string? period, string? department, string? status)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            if (!CheckHrOrAdmin())
            {
                TempData["Error"] = "Bạn không có quyền truy cập module này.";
                return RedirectToAction("Index", "Home");
            }

            var query = _context.PayrollPayments.AsQueryable();

            if (!string.IsNullOrEmpty(period))
                query = query.Where(p => p.PayrollPeriod == period);

            if (!string.IsNullOrEmpty(department))
                query = query.Where(p => p.Department == department);

            if (!string.IsNullOrEmpty(status) && int.TryParse(status, out int statusInt))
                query = query.Where(p => (int)p.Status == statusInt);

            var payments = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

            // Dữ liệu cho bộ lọc
            ViewBag.CurrentPeriod = period;
            ViewBag.CurrentDept = department;
            ViewBag.CurrentStatus = status;
            ViewBag.AllPeriods = await _context.PayrollPayments
                .Where(p => p.PayrollPeriod != null)
                .Select(p => p.PayrollPeriod)
                .Distinct()
                .OrderByDescending(p => p)
                .ToListAsync();
            ViewBag.AllDepts = await _context.Employees
                .Where(e => e.Department != null)
                .Select(e => e.Department)
                .Distinct()
                .ToListAsync();

            return View(payments);
        }

        // ================================================================
        // 2. TẠO ĐỢT THANH TOÁN - BƯỚC 1: Chọn nhân viên
        // ================================================================
        public async Task<IActionResult> Create(string? period, string? department)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            if (!CheckHrOrAdmin()) return RedirectToAction("Index", "Home");

            // Lấy danh sách lương hiện tại (chỉ ĐỌC, không ghi)
            var bangLuong = await LayDanhSachLuongHienTai(period);

            // Lấy danh sách nhân viên để ghép thông tin ngân hàng
            var allEmployees = await _context.Employees.ToListAsync();

            // Ghép thông tin ngân hàng vào bảng lương
            var salaryWithBankInfo = bangLuong.Select(s =>
            {
                var emp = allEmployees.FirstOrDefault(e => e.Id == s.EmployeeId);
                return new
                {
                    EmployeeId = s.EmployeeId,
                    EmployeeCode = emp?.EmployeeCode ?? "",
                    EmployeeName = s.EmployeeName ?? "",
                    Department = emp?.Department ?? "",
                    BankName = emp?.BankName ?? "",
                    BankAccountNumber = emp?.BankAccountNumber ?? "",
                    NetSalary = s.NetSalary,
                    HasBankInfo = !string.IsNullOrEmpty(emp?.BankName) && !string.IsNullOrEmpty(emp?.BankAccountNumber)
                };
            }).ToList();

            // Lọc theo phòng ban nếu có
            if (!string.IsNullOrEmpty(department))
                salaryWithBankInfo = salaryWithBankInfo.Where(s => s.Department == department).ToList();

            // Kiểm tra các nhân viên đã có payment trong kỳ này chưa
            List<int> alreadyPaidEmployeeIds = new List<int>();
            if (!string.IsNullOrEmpty(period))
            {
                alreadyPaidEmployeeIds = await _context.PayrollPayments
                    .Where(p => p.PayrollPeriod == period)
                    .Select(p => p.EmployeeId)
                    .ToListAsync();
            }

            ViewBag.SalaryWithBankInfo = salaryWithBankInfo;
            ViewBag.AlreadyPaidIds = alreadyPaidEmployeeIds;
            ViewBag.CurrentPeriod = period;
            ViewBag.CurrentDept = department;
            ViewBag.AllDepts = await _context.Employees
                .Where(e => e.Department != null)
                .Select(e => e.Department)
                .Distinct()
                .ToListAsync();

            return View();
        }

        // ================================================================
        // 3. TẠO ĐỢT THANH TOÁN - BƯỚC 2: Lưu payment
        // ================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePayment(List<int> selectedEmployeeIds, string payrollPeriod, string? paymentNote)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            if (!CheckHrOrAdmin()) return RedirectToAction("Index", "Home");

            if (string.IsNullOrEmpty(payrollPeriod))
            {
                TempData["Error"] = "Vui lòng nhập kỳ lương (ví dụ: T08/2026).";
                return RedirectToAction("Create");
            }

            if (selectedEmployeeIds == null || !selectedEmployeeIds.Any())
            {
                TempData["Error"] = "Vui lòng chọn ít nhất một nhân viên.";
                return RedirectToAction("Create");
            }

            // Lấy danh sách lương hiện tại theo kỳ
            var bangLuong = await LayDanhSachLuongHienTai(payrollPeriod);
            var allEmployees = await _context.Employees.ToListAsync();
            
            var cycle = PayrollCycleHelper.GetCycleByPeriod(payrollPeriod);

            int created = 0;
            int skippedDuplicate = 0;
            int skippedNoBank = 0;

            foreach (var empId in selectedEmployeeIds)
            {
                // Kiểm tra trùng: Employee + PayrollPeriod
                var exists = await _context.PayrollPayments
                    .AnyAsync(p => p.EmployeeId == empId && p.PayrollPeriod == payrollPeriod);

                if (exists)
                {
                    skippedDuplicate++;
                    continue;
                }

                var emp = allEmployees.FirstOrDefault(e => e.Id == empId);
                if (emp == null) continue;

                // Kiểm tra thông tin ngân hàng
                if (string.IsNullOrEmpty(emp.BankName) || string.IsNullOrEmpty(emp.BankAccountNumber))
                {
                    skippedNoBank++;
                    continue;
                }

                var salaryRecord = bangLuong.FirstOrDefault(s => s.EmployeeId == empId);
                double netSalary = salaryRecord?.NetSalary ?? 0;
                double workDays = (salaryRecord?.HoursWorked ?? 0) / 8.0;

                // --- SAVE SNAPSHOT ---
                if (salaryRecord != null)
                {
                    // Check if snapshot already exists
                    var existingSnapshot = await _context.Salaries
                        .FirstOrDefaultAsync(s => s.EmployeeId == empId && s.SalaryMonth == cycle.StartDate);
                        
                    if (existingSnapshot != null)
                    {
                        // Update existing snapshot
                        existingSnapshot.BasicSalary = salaryRecord.BasicSalary;
                        existingSnapshot.HoursWorked = salaryRecord.HoursWorked;
                        existingSnapshot.BonusAndAllowance = salaryRecord.BonusAndAllowance;
                        existingSnapshot.TotalSalary = salaryRecord.TotalSalary;
                        existingSnapshot.Insurance = salaryRecord.Insurance;
                        existingSnapshot.TaxTNCN = salaryRecord.TaxTNCN;
                        existingSnapshot.NetSalary = salaryRecord.NetSalary;
                        // NotMapped properties are ignored by EF, but that's fine because we don't save them anyway.
                    }
                    else
                    {
                        // Add new snapshot
                        _context.Salaries.Add(new Salary
                        {
                            EmployeeId = salaryRecord.EmployeeId,
                            EmployeeName = salaryRecord.EmployeeName,
                            BasicSalary = salaryRecord.BasicSalary,
                            HoursWorked = salaryRecord.HoursWorked,
                            BonusAndAllowance = salaryRecord.BonusAndAllowance,
                            TotalSalary = salaryRecord.TotalSalary,
                            Insurance = salaryRecord.Insurance,
                            TaxTNCN = salaryRecord.TaxTNCN,
                            NetSalary = salaryRecord.NetSalary,
                            SalaryMonth = cycle.StartDate
                        });
                    }
                }

                var payment = new PayrollPayment
                {
                    EmployeeId = emp.Id,
                    EmployeeCode = emp.EmployeeCode ?? "",
                    EmployeeName = emp.Name ?? "",
                    Department = emp.Department ?? "",
                    BankName = emp.BankName,
                    BankAccountNumber = emp.BankAccountNumber,
                    NetSalary = netSalary,  // Chỉ đọc, không tính lại
                    PayrollPeriod = payrollPeriod,
                    Status = PaymentStatus.ChoThanhToan,
                    CreatedAt = DateTime.Now,
                    PaidAt = null,
                    PaymentNote = paymentNote,
                    CycleStartDate = cycle.StartDate,
                    CycleEndDate = cycle.EndDate,
                    PaymentDate = cycle.PaymentDate,
                    WorkDays = workDays
                };

                _context.PayrollPayments.Add(payment);
                created++;
            }

            await _context.SaveChangesAsync();

            string msg = $"✅ Đã tạo {created} payment.";
            if (skippedDuplicate > 0) msg += $" ⚠️ {skippedDuplicate} bản ghi bị bỏ qua (đã tồn tại trong kỳ này).";
            if (skippedNoBank > 0) msg += $" ❌ {skippedNoBank} nhân viên bị bỏ qua vì thiếu thông tin ngân hàng.";

            TempData["Success"] = msg;
            return RedirectToAction("Index", new { period = payrollPeriod });
        }

        // ================================================================
        // 4. ĐÁNH DẤU CHỜ THANH TOÁN
        // ================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPending(int id)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            if (!CheckHrOrAdmin()) return Json(new { success = false, message = "Không có quyền." });

            var payment = await _context.PayrollPayments.FindAsync(id);
            if (payment == null) return Json(new { success = false, message = "Không tìm thấy bản ghi." });

            if (payment.Status == PaymentStatus.DaThanhToan)
                return Json(new { success = false, message = "Không thể thay đổi trạng thái đã thanh toán." });

            payment.Status = PaymentStatus.ChoThanhToan;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // ================================================================
        // 5. ĐÁNH DẤU ĐÃ THANH TOÁN - Chỉ HR/Admin xác nhận sau khi ngân hàng hoàn tất
        // ================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPaid(int id)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            if (!CheckHrOrAdmin()) return Json(new { success = false, message = "Không có quyền." });

            var payment = await _context.PayrollPayments.FindAsync(id);
            if (payment == null) return Json(new { success = false, message = "Không tìm thấy bản ghi." });

            // Chỉ cho phép chuyển từ ChoThanhToan → DaThanhToan
            if (payment.Status != PaymentStatus.ChoThanhToan)
                return Json(new { success = false, message = "Chỉ có thể xác nhận thanh toán khi trạng thái là 'Chờ thanh toán'." });

            // === TRANSACTION: cập nhật payment + tạo notification ===
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                payment.Status = PaymentStatus.DaThanhToan;
                payment.PaidAt = DateTime.Now;  // Chỉ ghi PaidAt khi thực sự xác nhận thanh toán
                payment.ConfirmedAt = DateTime.Now;
                payment.ConfirmedBy = HttpContext.Session.GetString("RealName") ?? HttpContext.Session.GetString("User") ?? "Hệ thống HR";
                await _context.SaveChangesAsync();

                // Tạo notification cho nhân viên (in-memory, khớp với cơ chế hiện tại)
                string noiDung = $"💰 Lương kỳ {payment.PayrollPeriod} của bạn đã được công ty xác nhận thanh toán.\nSố tiền thực nhận: {payment.NetSalary:N0} ₫";
                string nguoiNhan = payment.EmployeeName ?? "";
                string nguoiGui = HttpContext.Session.GetString("RealName") ?? HttpContext.Session.GetString("User") ?? "Hệ thống HR";

                var senderId = await _context.Users.Where(x => x.Username == HttpContext.Session.GetString("User"))
                    .Select(x => (int?)x.Id).FirstOrDefaultAsync();
                var createdNotifications = await _notifications.SendAsync(
                    nguoiNhan, noiDung, "SALARY_PAID",
                    senderUserId: senderId, senderName: nguoiGui,
                    deduplicationKey: $"salary-paid:{payment.Id}");

                await transaction.CommitAsync();

                return Json(new { success = true, paidAt = payment.PaidAt?.ToString("dd/MM/yyyy HH:mm"), notified = createdNotifications.Count > 0 });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // ================================================================
        // 6. ĐÁNH DẤU HÀNG LOẠT ĐÃ THANH TOÁN
        // ================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkMultipleAsPaid(List<int> paymentIds)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            if (!CheckHrOrAdmin()) return Json(new { success = false, message = "Không có quyền." });

            if (paymentIds == null || !paymentIds.Any())
                return Json(new { success = false, message = "Không có bản ghi nào được chọn." });

            // Chỉ lấy những payment đang ChoThanhToan (chống xác nhận trùng)
            var payments = await _context.PayrollPayments
                .Where(p => paymentIds.Contains(p.Id) && p.Status == PaymentStatus.ChoThanhToan)
                .ToListAsync();

            if (!payments.Any())
                return Json(new { success = false, message = "Tất cả các bản ghi đã được thanh toán trước đó." });

            string nguoiGui = HttpContext.Session.GetString("RealName") ?? HttpContext.Session.GetString("User") ?? "Hệ thống HR";

            // === TRANSACTION: cập nhật tất cả payment + tạo notification riêng từng cái ===
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var notified = 0;
                var senderId = await _context.Users.Where(x => x.Username == HttpContext.Session.GetString("User"))
                    .Select(x => (int?)x.Id).FirstOrDefaultAsync();

                foreach (var p in payments)
                {
                    p.Status = PaymentStatus.DaThanhToan;
                    p.PaidAt = DateTime.Now;
                    p.ConfirmedAt = DateTime.Now;
                    p.ConfirmedBy = nguoiGui;

                    // Tạo notification riêng cho từng payment (KHÔNG gộp kỳ lương)
                    string noiDung = $"💰 Lương kỳ {p.PayrollPeriod} của bạn đã được công ty xác nhận thanh toán.\nSố tiền thực nhận: {p.NetSalary:N0} ₫";
                    var created = await _notifications.SendAsync(
                        p.EmployeeName ?? string.Empty, noiDung, "SALARY_PAID",
                        senderUserId: senderId, senderName: nguoiGui,
                        deduplicationKey: $"salary-paid:{p.Id}");
                    notified += created.Count;
                }

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return Json(new { success = true, count = payments.Count, notified });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // ================================================================
        // 7. XUẤT FILE CSV CHUYỂN KHOẢN
        // Xuất file KHÔNG đồng nghĩa với đã thanh toán.
        // Status KHÔNG thay đổi khi xuất file.
        // ================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportCsv(List<int> paymentIds, string? payrollPeriod)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            if (!CheckHrOrAdmin()) return Unauthorized();

            List<PayrollPayment> payments;

            if (paymentIds != null && paymentIds.Any())
            {
                // Xuất các payment được chọn
                payments = await _context.PayrollPayments
                    .Where(p => paymentIds.Contains(p.Id))
                    .OrderBy(p => p.EmployeeCode)
                    .ToListAsync();
            }
            else if (!string.IsNullOrEmpty(payrollPeriod))
            {
                // Xuất theo kỳ lương (chỉ chưa thanh toán hoặc chờ thanh toán)
                payments = await _context.PayrollPayments
                    .Where(p => p.PayrollPeriod == payrollPeriod && p.Status != PaymentStatus.DaThanhToan)
                    .OrderBy(p => p.EmployeeCode)
                    .ToListAsync();
            }
            else
            {
                TempData["Error"] = "Vui lòng chọn nhân viên hoặc kỳ lương để xuất file.";
                return RedirectToAction("Index");
            }

            // Lọc: chỉ xuất nhân viên có đủ thông tin ngân hàng
            var validPayments = payments.Where(p =>
                !string.IsNullOrEmpty(p.BankName) &&
                !string.IsNullOrEmpty(p.BankAccountNumber)).ToList();

            if (!validPayments.Any())
            {
                TempData["Error"] = "Không có nhân viên nào đủ điều kiện xuất file (thiếu thông tin ngân hàng).";
                return RedirectToAction("Index");
            }

            // Tạo CSV - KHÔNG thay đổi Status, KHÔNG ghi PaidAt
            var sb = new StringBuilder();
            // Header BOM để Excel hiển thị tiếng Việt đúng
            sb.AppendLine("Mã NV,Họ tên,Số tài khoản,Ngân hàng,Số tiền (VNĐ),Nội dung chuyển khoản");

            foreach (var p in validPayments)
            {
                string period = p.PayrollPeriod ?? "KyLuong";
                string noiDung = $"Luong {period} {p.EmployeeCode}";
                // Số tiền làm tròn về số nguyên
                long soTien = (long)Math.Round(p.NetSalary);

                // Dùng dấu nháy kép để tránh lỗi nếu có dấu phẩy trong tên
                sb.AppendLine($"\"{p.EmployeeCode}\",\"{p.EmployeeName}\",\"{p.BankAccountNumber}\",\"{p.BankName}\",{soTien},\"{noiDung}\"");
            }

            // Tên file
            string fileName = $"ChuyenKhoan_{payrollPeriod?.Replace("/", "-") ?? DateTime.Now.ToString("yyyyMM")}_{DateTime.Now:ddMMyyHHmm}.csv";
            byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();

            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        // ================================================================
        // 8. XÓA PAYMENT (chỉ khi chưa thanh toán)
        // ================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            if (!CheckHrOrAdmin()) return Json(new { success = false, message = "Không có quyền." });

            var payment = await _context.PayrollPayments.FindAsync(id);
            if (payment == null) return Json(new { success = false, message = "Không tìm thấy bản ghi." });

            if (payment.Status == PaymentStatus.DaThanhToan)
                return Json(new { success = false, message = "Không thể xóa bản ghi đã thanh toán." });

            _context.PayrollPayments.Remove(payment);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
    }
}
