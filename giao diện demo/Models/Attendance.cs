using System;

namespace giao_dien_demo.Models
{
    public class Attendance
    {
        // --- CÁC TRƯỜNG DỮ LIỆU CŨ (GIỮ NGUYÊN) ---
        public int Id { get; set; }
        public string? EmployeeNameSnapshot { get; set; }
        
        public int? EmployeeId { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }
        
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string? EmployeeName { get => EmployeeNameSnapshot; set => EmployeeNameSnapshot = value; }
        public DateTime CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string? EmployeeCode { get; set; }

        // =========================================================
        // 🔥 CÁC TRƯỜNG MỚI ĐỂ XỬ LÝ CHẤM CÔNG CHUYÊN SÂU (GIỜ HÀNH CHÍNH / TĂNG CA)
        // =========================================================

        // Số giờ làm hành chính (Sáng 7h30-11h30, Chiều 13h-17h)
        public double StandardHours { get; set; }

        // Số giờ tăng ca (Sau 17h hoặc làm ngày Chủ nhật)
        public double OvertimeHours { get; set; }

        // Số phút đi muộn để lưu vết
        public int LateMinutes { get; set; }

        // Ghi chú trạng thái (VD: Đi muộn sáng, Đi muộn chiều, Làm thêm Chủ Nhật, Đúng giờ...)
        public string? StatusNote { get; set; }

        // =========================================================
        // 🔥 WORKFLOW YÊU CẦU ĐIỀU CHỈNH CHẤM CÔNG (PENDING -> APPROVED -> APPLIED)
        // =========================================================
        
        public string? AdjustmentStatus { get; set; } // PENDING, APPROVED, REJECTED, APPLIED
        public TimeSpan? RequestedCheckOutTime { get; set; }
        public string? AdjustmentReason { get; set; }
    }
}