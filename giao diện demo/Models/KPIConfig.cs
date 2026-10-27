using System.ComponentModel.DataAnnotations;

namespace giao_dien_demo.Models
{
    public class KPIConfig
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string KPIRating { get; set; } // A, B, C, D... hoặc tên loại
        
        public decimal Coefficient { get; set; } = 1.0m; // Hệ số
        
        public string? Notes { get; set; } // Ghi chú
    }
}
