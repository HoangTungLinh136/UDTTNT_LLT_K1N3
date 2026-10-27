using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class KPIPeriod
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "PLANNED"; // PLANNED, ACTIVE, CLOSED

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        [StringLength(100)]
        public string? CreatedBy { get; set; }
    }
}
