using System;

namespace giao_dien_demo.Models
{
    public class Notification
    {
        public int Id { get; set; }

        public string? Message { get; set; }

        // Tự động gán thời gian hiện tại khi tạo thông báo mới
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false;

        // Canonical ownership key used by every production read/mutation.
        public int RecipientUserId { get; set; }
        public int? SenderUserId { get; set; }
        public string? SenderName { get; set; }
        public string? DeduplicationKey { get; set; }

        // 🔥 QUAN TRỌNG: Thêm trường này để biết thông báo gửi cho ai
        // Có thể lưu tên tài khoản (linh.employee) hoặc tên thật (Bàn Thị Linh)
        public string? NguoiNhan { get; set; }

        // --- CÁC TRƯỜNG MỚI ĐỂ ĐIỀU HƯỚNG ---
        public string? NotificationType { get; set; }
        public int? ReferenceId { get; set; }
        public string? TargetUrl { get; set; }
    }
}
