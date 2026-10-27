using System;
using System.ComponentModel.DataAnnotations;

namespace giao_dien_demo.Models
{
    public class MonitoringAlert
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(50)]
        public string Severity { get; set; } // Info, Low, Medium, High, Critical

        [MaxLength(200)]
        public string AlertName { get; set; } // Ví dụ: "CPU High"

        [MaxLength(100)]
        public string MetricName { get; set; } // "CPU"

        [MaxLength(50)]
        public string CurrentValue { get; set; } // "95%"

        [MaxLength(50)]
        public string Threshold { get; set; } // "> 90%"

        public DateTime StartedAt { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } // Active, Acknowledged, Resolved

        public DateTime? AcknowledgedAt { get; set; }

        [MaxLength(100)]
        public string AcknowledgedBy { get; set; }

        public DateTime? ResolvedAt { get; set; }
    }
}
