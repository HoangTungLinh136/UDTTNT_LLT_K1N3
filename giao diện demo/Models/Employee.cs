using System;

namespace giao_dien_demo.Models
{
    public class Employee
    {
        public int Id { get; set; }
        public string? EmployeeCode { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        
        public string? Status { get; set; } = "Active";

        // --- HỒ SƠ CHI TIẾT ---
        public string? AvatarPath { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? Gender { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? CitizenId { get; set; }
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }

        // --- TRÌNH ĐỘ HỌC VẤN & KINH NGHIỆM ---
        public string? AcademicLevel { get; set; } // Thạc sĩ, Cử nhân, Cao đẳng...
        public decimal QualificationAllowance { get; set; } = 0; // Phụ cấp theo bằng cấp
        public int ExperienceYears { get; set; } = 0; // Số năm kinh nghiệm

        // --- CÔNG VIỆC ---
        public string? Department { get; set; }
        public string? Position { get; set; } // Trạng thái: Chính thức, Thử việc...
        public string? JobTitle { get; set; } // Vai trò: Admin, HR, Leader, Nhân viên...
        public string? KPIScore { get; set; } // Điểm/Loại KPI (A, B, C...)

        // --- LƯƠNG & PHỤ CẤP ---
        public decimal Salary { get; set; }
        public decimal PositionAllowance { get; set; } = 0;
        public decimal HazardousAllowance { get; set; } = 0;
        public decimal RegionalAllowanceSystem { get; set; } = 0;

        // --- LOGIC TỰ ĐỘNG (ĐÃ CẬP NHẬT CỘNG THÊM PHỤ CẤP BẰNG CẤP) ---
        public decimal TotalGrossSalary =>
            Salary +
            PositionAllowance +
            HazardousAllowance +
            (Salary * RegionalAllowanceSystem) +
            QualificationAllowance; // Tự động cộng thêm tiền bằng cấp vào đây

        public decimal HourlyRate => Salary > 0 ? Math.Round(Salary / 208m, 0) : 0;
    }
}