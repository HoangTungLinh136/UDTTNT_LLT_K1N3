using System;

namespace giao_dien_demo.Models.AI
{
    /// <summary>
    /// Long-term Personal Memory của AVA theo từng user.
    ///
    /// Đây KHÔNG phải nơi lưu dữ liệu nghiệp vụ biến động như:
    /// - lương
    /// - chấm công
    /// - số ngày phép
    /// - quyền hiện tại
    ///
    /// Những dữ liệu đó vẫn phải truy vấn từ backend / SQL.
    /// </summary>
    public sealed class UserMemory
    {
        public long Id { get; set; }

        /// <summary>
        /// UserId được backend ASP.NET xác định sau authentication.
        /// Không nhận trực tiếp từ browser.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// EmployeeId dạng text để không phụ thuộc kiểu khóa Employee hiện tại.
        /// Có thể null nếu tài khoản không gắn Employee.
        /// </summary>
        public string? EmployeeId { get; set; }

        /// <summary>
        /// Loại memory.
        ///
        /// Ví dụ:
        /// response_style
        /// work_context
        /// preference
        /// frequent_task
        /// department_context
        /// </summary>
        public string MemoryType { get; set; } = string.Empty;

        /// <summary>
        /// Khóa logic của memory.
        ///
        /// Ví dụ:
        /// preferred_answer_length
        /// preferred_language
        /// main_work_area
        /// </summary>
        public string MemoryKey { get; set; } = string.Empty;

        /// <summary>
        /// Nội dung memory đã được làm sạch.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Độ tin cậy từ 0.0 đến 1.0.
        /// Phase 04 Memory Extractor sẽ xác định giá trị này.
        /// </summary>
        public double Confidence { get; set; } = 1.0;

        /// <summary>
        /// Nguồn tạo memory.
        ///
        /// conversation
        /// manual
        /// system
        /// </summary>
        public string Source { get; set; } = "conversation";

        /// <summary>
        /// Conversation tạo ra memory nếu có.
        /// </summary>
        public string? SourceConversationId { get; set; }

        /// <summary>
        /// Memory có còn được phép sử dụng hay không.
        /// Không hard-delete ngay để tránh mất kiểm soát lịch sử.
        /// </summary>
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
