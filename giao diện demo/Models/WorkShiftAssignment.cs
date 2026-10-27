using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class WorkShiftAssignment
    {
        public int Id { get; set; }

        public int WorkShiftId { get; set; }
        
        [ForeignKey("WorkShiftId")]
        public virtual WorkShift? WorkShift { get; set; }

        [Required]
        [StringLength(50)]
        public string TargetType { get; set; } = ""; // Employee, Department, Company

        [StringLength(100)]
        public string TargetValue { get; set; } = ""; // e.g., EmployeeCode, DepartmentName, or empty for Company

        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
    }
}
