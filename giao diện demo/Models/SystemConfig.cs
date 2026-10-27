using System;
using System.ComponentModel.DataAnnotations;

namespace giao_dien_demo.Models
{
    public class SystemConfig
    {
        [Key]
        [MaxLength(100)]
        public string Key { get; set; }

        public string Value { get; set; }

        [MaxLength(50)]
        public string Category { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [MaxLength(100)]
        public string UpdatedBy { get; set; }
    }
}
