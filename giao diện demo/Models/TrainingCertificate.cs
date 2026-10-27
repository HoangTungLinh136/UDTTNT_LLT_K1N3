using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    [Table("TrainingCertificates")]
    public class TrainingCertificate
    {
        [Key]
        public int Id { get; set; }

        public int EnrollmentId { get; set; }
        [ForeignKey(nameof(EnrollmentId))]
        public CourseEnrollment? Enrollment { get; set; }

        public int EmployeeId { get; set; }
        [ForeignKey(nameof(EmployeeId))]
        public Employee? Employee { get; set; }

        [Required, StringLength(200)]
        public string EmployeeNameSnapshot { get; set; } = string.Empty;

        [Column("IssuedAt")]
        public DateTime IssuedDate { get; set; } = DateTime.Now;

        public DateTime? ExpiresAt { get; set; }

        [Required, StringLength(200)]
        public string CertificateNumber { get; set; } = string.Empty;

        // Compatibility with current protected-file code.
        [Column("OriginalFileName"), StringLength(520)]
        public string? CertificateUrl { get; set; }

        [Column("StoredFileName"), StringLength(520)]
        public string? CanonicalFileId { get; set; }

        public bool IsPublished { get; set; } = true;
    }
}
