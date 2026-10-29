using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    [Table("EmployeeReportingHistory")]
    public class EmployeeReportingHistory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        public int? PreviousManagerId { get; set; }

        public int? NewManagerId { get; set; }

        [Required]
        [StringLength(500)]
        public string ChangeReason { get; set; } = string.Empty;

        public DateTime EffectiveDate { get; set; }

        public int ChangedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigations
        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        [ForeignKey("PreviousManagerId")]
        public Employee? PreviousManager { get; set; }

        [ForeignKey("NewManagerId")]
        public Employee? NewManager { get; set; }

        [ForeignKey("ChangedByUserId")]
        public User? ChangedByUser { get; set; }
    }
}

