using System;

namespace giao_dien_demo.Models
{
    public class OffboardingHistory
    {
        public int Id { get; set; }
        public int OffboardingRecordId { get; set; }
        public OffboardingRecord? OffboardingRecord { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string? Actor { get; set; }
        public string? Action { get; set; }
        public string? Details { get; set; }
    }
}
