using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class AttendanceAdjustmentAttachment
    {
        public int Id { get; set; }
        
        public int RequestId { get; set; } // Liên kết tới AttendanceAdjustmentRequest
        [ForeignKey("RequestId")]
        public AttendanceAdjustmentRequest? AttendanceAdjustmentRequest { get; set; }
        
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        
        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }
}
