using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace giao_dien_demo.Models
{
    public class RewardHistory
    {
        [Key]
        public int Id { get; set; }

        public int RewardRecordId { get; set; }

        [ForeignKey("RewardRecordId")]
        [JsonIgnore]
        public RewardRecord? RewardRecord { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;

        // Người thực hiện hành động
        public string? Actor { get; set; }

        // Loại action: CREATE, SUBMIT, APPROVE, REJECT, CANCEL, SYNC_PAYROLL, UPDATE...
        public string? Action { get; set; }

        // Mô tả chi tiết hành động
        public string? Details { get; set; }
    }
}
