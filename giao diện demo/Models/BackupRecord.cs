using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class BackupRecord
    {
        [Key]
        public long Id { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; }

        [Required]
        [StringLength(1000)]
        public string FilePath { get; set; } // Logical path

        [Required]
        [StringLength(50)]
        public string Type { get; set; } // Full, DatabaseOnly

        public long SizeBytes { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } // Success, Failed, InProgress

        public DateTime CreatedAt { get; set; }

        [StringLength(100)]
        public string CreatedBy { get; set; } // Admin username or System

        public DateTime? CompletedAt { get; set; }

        public string FailureReason { get; set; }

        [StringLength(64)]
        public string Checksum { get; set; } // SHA-256

        public bool IsDeleted { get; set; } // Soft delete flag
    }
}
