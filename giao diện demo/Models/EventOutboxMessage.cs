using System;
using System.ComponentModel.DataAnnotations;

namespace giao_dien_demo.Models
{
    public class EventOutboxMessage
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string EventCode { get; set; }
        
        [Required]
        public string PayloadJson { get; set; }
        
        [StringLength(100)]
        public string? RelatedUsername { get; set; }
        
        [StringLength(100)]
        public string? RelatedManagerUsername { get; set; }
        
        [StringLength(100)]
        public string? IdempotencyKey { get; set; }
        
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "PENDING"; // PENDING, PROCESSED, FAILED
        
        public int RetryCount { get; set; } = 0;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime? LastAttemptAt { get; set; }
        
        public string? LastError { get; set; }
    }
}
