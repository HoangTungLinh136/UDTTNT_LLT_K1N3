using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class BackupConfig
    {
        [Key]
        public int Id { get; set; }

        public bool IsEnabled { get; set; }

        [Required]
        [StringLength(50)]
        public string Frequency { get; set; } // "Daily", "Weekly", "Monthly"

        public TimeSpan ExecuteTime { get; set; }

        public int RetentionDays { get; set; }

        [Required]
        [StringLength(50)]
        public string BackupType { get; set; } // "Full", "DatabaseOnly"

        public DateTime? LastRunTime { get; set; }
    }
}
