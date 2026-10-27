using System.ComponentModel.DataAnnotations;

namespace giao_dien_demo.Models
{
    public class JobTitleConfig
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string JobTitle { get; set; } // Giám đốc, Trưởng phòng, Nhân viên...
        
        public decimal Coefficient { get; set; } = 1.0m; // Hệ số
        
        public string? Notes { get; set; } // Ghi chú
    }
}
