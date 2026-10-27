using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class IncidentEvidence
    {
        [Key]
        public int Id { get; set; }

        public int IncidentRecordId { get; set; }
        [ForeignKey("IncidentRecordId")]
        public IncidentRecord? IncidentRecord { get; set; }

        [Required]
        public string? FileName { get; set; }
        
        [Required]
        public string? FilePath { get; set; }
        
        public string? ContentType { get; set; }
        public string? Description { get; set; }
        
        public string? UploadedBy { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }
}
