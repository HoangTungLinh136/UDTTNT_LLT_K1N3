using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    // Enum trạng thái thanh toán
    public enum PaymentStatus
    {
        ChuaThanhToan = 0,  // Chưa thanh toán
        ChoThanhToan = 1,   // Chờ thanh toán
        DaThanhToan = 2     // Đã thanh toán
    }

    /// <summary>
    /// Model lưu thông tin thanh toán lương.
    /// Chỉ lưu dữ liệu phục vụ thanh toán, KHÔNG lưu công thức tính lương.
    /// NetSalary được lấy từ SalaryController tại thời điểm tạo payment.
    /// </summary>
    public class PayrollPayment
    {
        public int Id { get; set; }

        // --- THÔNG TIN NHÂN VIÊN ---
        public int EmployeeId { get; set; }
        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        public string? EmployeeCode { get; set; }
        public string? EmployeeName { get; set; }
        public string? Department { get; set; }

        // --- THÔNG TIN NGÂN HÀNG (lấy từ Employee tại thời điểm tạo payment) ---
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }

        // --- THÔNG TIN THANH TOÁN ---
        /// <summary>
        /// Lương thực nhận. Lấy từ SalaryController.NetSalary. KHÔNG tự tính lại.
        /// </summary>
        public double NetSalary { get; set; }

        /// <summary>
        /// Kỳ lương do HR nhập tay, ví dụ: "T08/2026"
        /// </summary>
        public string? PayrollPeriod { get; set; }

        // --- TRẠNG THÁI ---
        public PaymentStatus Status { get; set; } = PaymentStatus.ChoThanhToan;

        // --- THỜI GIAN ---
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Chỉ được ghi khi Status chuyển sang DaThanhToan.
        /// Xuất file KHÔNG được ghi PaidAt.
        /// </summary>
        public DateTime? PaidAt { get; set; }

        // --- GHI CHÚ ---
        public string? PaymentNote { get; set; }
        // --- THÔNG TIN CHU KỲ (THÊM MỚI) ---
        public DateTime CycleStartDate { get; set; }
        public DateTime CycleEndDate { get; set; }
        public double WorkDays { get; set; }
        public DateTime PaymentDate { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public string? ConfirmedBy { get; set; }
    }
}
