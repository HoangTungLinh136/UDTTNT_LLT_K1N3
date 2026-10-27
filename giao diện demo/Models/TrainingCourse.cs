using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    [Table("TrainingCourses")]
    public class TrainingCourse
    {
        [Key]
        public int Id { get; set; }

        public int? ProgramId { get; set; }

        [Required, StringLength(100)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(400)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Category { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Skill { get; set; }

        [StringLength(4000)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? DurationHours { get; set; } = 0m;

        public DateTime? StartDate { get; set; } = DateTime.Today;
        public DateTime? EndDate { get; set; } = DateTime.Today.AddYears(1);
        public DateTime? RegistrationStart { get; set; } = DateTime.Today;
        public DateTime? RegistrationEnd { get; set; } = DateTime.Today.AddYears(1);

        public int? Capacity { get; set; }
        public int? PrerequisiteCourseId { get; set; }

        [StringLength(400)]
        public string? EligibleDepartment { get; set; }

        [StringLength(400)]
        public string? EligiblePosition { get; set; }

        [Column("InstructorName"), StringLength(400)]
        public string? Instructor { get; set; }

        // Compatibility alias: old UI used Mode; current DB uses Schedule.
        [Column("Schedule"), StringLength(2000)]
        public string? Mode { get; set; }

        public bool RequiresApproval { get; set; }
        public bool IsPublished { get; set; } = true;

        [Required, StringLength(60)]
        public string Status { get; set; } = "Open";

        // Current DB has no IsMandatory column.
        [NotMapped]
        public bool IsMandatory { get; set; }

        public ICollection<CourseEnrollment> Enrollments { get; set; } = new List<CourseEnrollment>();
        public ICollection<TrainingMaterial> Materials { get; set; } = new List<TrainingMaterial>();
    }
}
