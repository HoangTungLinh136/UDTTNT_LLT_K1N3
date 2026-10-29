using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    [Table("Departments")]
    public class Department
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty; // HR, IT, MKT, FIN, SALES, OPS (CR-01)

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty; // Official Vietnamese display name (CR-01)

        [StringLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true; // Soft deactivation only (OD-13)

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? CreatedByUserId { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int? UpdatedByUserId { get; set; }

        // Navigation
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}

