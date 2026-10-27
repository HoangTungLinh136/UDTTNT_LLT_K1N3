using System;
using System.ComponentModel.DataAnnotations;

namespace giao_dien_demo.Models
{
    public class SystemErrorLog
    {
        [Key]
        public int Id { get; set; }

        public DateTime Timestamp { get; set; }

        [MaxLength(20)]
        public string Level { get; set; } // Error, Warning, Fatal

        [MaxLength(100)]
        public string Service { get; set; } // WebApp, Database, BackgroundAgent

        [MaxLength(200)]
        public string ExceptionType { get; set; }

        public string Message { get; set; }

        public string StackTrace { get; set; }

        [MaxLength(100)]
        public string RequestId { get; set; }

        [MaxLength(100)]
        public string CorrelationId { get; set; }

        [MaxLength(100)]
        public string User { get; set; } // Nếu có

        [MaxLength(50)]
        public string IP { get; set; }

        [MaxLength(500)]
        public string UserAgent { get; set; }
    }
}
