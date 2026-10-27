using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    [Table("AuditLogs")]
    public class AuditLog
    {
        [Key]
        public long Id { get; set; }

        [Required]
        [Column("Timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.Now;

        [Required]
        [StringLength(100)]
        [Column("Action")]
        public string Action { get; set; } = string.Empty;

        [Column("EntityName")]
        public string Resource { get; set; } = string.Empty;

        [Column("EntityId")]
        public string ResourceId { get; set; } = string.Empty;

        [Column("Details")]
        public string Details { get; set; } = string.Empty;

        [Column("Username")]
        public string Username { get; set; } = string.Empty;

        // =====================================================
        // Compatibility properties:
        // DB legacy không có các cột này.
        // Giữ lại để code hiện tại compile và sử dụng trong memory.
        // =====================================================

        [NotMapped]
        public string UserId { get; set; } = string.Empty;

        [NotMapped]
        public string Role { get; set; } = string.Empty;

        [NotMapped]
        public string Module { get; set; } = string.Empty;

        [NotMapped]
        public string EventType { get; set; } = string.Empty;

        [NotMapped]
        public string Status { get; set; } = string.Empty;

        [NotMapped]
        public string IPAddress { get; set; } = string.Empty;

        [NotMapped]
        public string UserAgent { get; set; } = string.Empty;

        [NotMapped]
        public string OldValues { get; set; } = string.Empty;

        [NotMapped]
        public string NewValues { get; set; } = string.Empty;

        [NotMapped]
        public string Changes { get; set; } = string.Empty;

        [NotMapped]
        public string FailureReason { get; set; } = string.Empty;

        [NotMapped]
        public string RequestId { get; set; } = string.Empty;

        [NotMapped]
        public string CorrelationId { get; set; } = string.Empty;
    }
}

