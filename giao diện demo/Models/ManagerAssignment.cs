using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    /// <summary>
    /// Canonical relationship: Manager → Managed Organizational Unit.
    /// A Manager (User with Role="Manager") can manage one or more organizational units.
    /// This replaces the implicit hard-coded Employee.Department scope.
    /// </summary>
    public class ManagerAssignment
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The User being assigned as Manager of an organizational unit.
        /// </summary>
        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        /// <summary>
        /// The organizational unit (department) this user manages.
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
        public bool IsActive => EffectiveFrom <= DateTime.Now
            && (EffectiveTo == null || EffectiveTo > DateTime.Now);
    }
}
