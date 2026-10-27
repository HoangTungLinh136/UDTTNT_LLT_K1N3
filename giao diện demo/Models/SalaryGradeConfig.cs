using System.ComponentModel.DataAnnotations;

namespace giao_dien_demo.Models
{
    public class SalaryGradeConfig
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập hoặc chọn trình độ")]
        public string AcademicLevel { get; set; } = "";

        public int SalaryRank { get; set; } = 1;

        public decimal Coefficient { get; set; } = 1.0m;

        public decimal SuggestedAllowance { get; set; } = 0;

        public string? Notes { get; set; }
    }
}
