using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class IncidentHistory
    {
        [Key]
        public int Id { get; set; }

        public int IncidentRecordId { get; set; }
        [ForeignKey("IncidentRecordId")]
        public IncidentRecord? IncidentRecord { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string? Actor { get; set; }
        public string? Action { get; set; }
        public string? Details { get; set; }
    }
}
