using System.ComponentModel.DataAnnotations;

namespace giao_dien_demo.Models;

public sealed class EmployeeCreateViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(200, ErrorMessage = "Họ và tên không được vượt quá 200 ký tự.")]
    [Display(Name = "Tên nhân viên")]
    public string? Name { get; set; }

    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? Gender { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? CitizenId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập phòng ban.")]
    public string? Department { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn trạng thái làm việc.")]
    public string? Position { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn chức danh.")]
    public string? JobTitle { get; set; }

    public string? AcademicLevel { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Lương không được âm.")]
    public decimal Salary { get; set; }
}
