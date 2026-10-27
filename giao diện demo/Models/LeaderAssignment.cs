using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    /// <summary>
    /// Canonical relationship: Leader → Managed Organizational Unit.
    /// A Leader (User with Role="Leader") can oversee one or more organizational units.
    /// </summary>
    public class LeaderAssignment
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The User being assigned as Leader of an organizational unit.
        /// </summary>
        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        /// <summary>
        /// The organizational unit (department) this user oversees.
        /// References Employee.Department values.
        /// </summary>
        [Required]
        public string OrganizationalUnit { get; set; } = string.Empty;

        /// <summary>
        /// When this assignment becomes effective.
        /// </summary>
        public DateTime EffectiveFrom { get; set; } = DateTime.Now;

        /// <summary>
        /// When this assignment ends. null = still active.
        /// </summary>
        public DateTime? EffectiveTo { get; set; }

        /// <summary>
        /// Who created this assignment (admin username).
        /// </summary>
        public string? AssignedBy { get; set; }

        /// <summary>
        /// When this assignment was created.
        /// </summary>
        public DateTime AssignedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Whether this assignment is currently active.
        /// </summary>
        [NotMapped]
        public bool IsActive => EffectiveTo == null || EffectiveTo > DateTime.Now;
    }
}
