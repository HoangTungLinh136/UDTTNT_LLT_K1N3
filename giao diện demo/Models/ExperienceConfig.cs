using System.ComponentModel.DataAnnotations;

namespace giao_dien_demo.Models
{
    public class ExperienceConfig
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int MinYears { get; set; } // Từ X năm
        
        [Required]
        public int MaxYears { get; set; } // Đến Y năm
        
        public decimal Coefficient { get; set; } = 1.0m; // Hệ số
        
        public string? Notes { get; set; } // Ghi chú (VD: Dưới 1 năm, 1-3 năm)
    }
}
