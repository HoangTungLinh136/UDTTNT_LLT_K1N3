using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class KPITarget
    {
        [Key]
        public int Id { get; set; }

        public int EvaluationId { get; set; }
        [ForeignKey("EvaluationId")]
        public KPIEvaluation? Evaluation { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal Weight { get; set; } // 0-100

        [Column(TypeName = "decimal(5,2)")]
        public decimal Progress { get; set; } = 0; // 0-100

        [Column(TypeName = "decimal(5,2)")]
        public decimal? ManagerScore { get; set; } // 0-100

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
