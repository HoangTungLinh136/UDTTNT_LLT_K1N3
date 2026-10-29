using System;
using System.Collections.Generic;

namespace giao_dien_demo.Models
{
    public class EmployeePayrollViewModel
    {
        public bool HasData { get; set; } = true;
        public string? EmployeeCode { get; set; }
        public string? EmployeeName { get; set; }
        public string? Department { get; set; }

        // Bộ lọc
        public int? SelectedYear { get; set; }
        public string? SelectedPeriod { get; set; }
        public string? SelectedStatus { get; set; }

        public List<int> AvailableYears { get; set; } = new();
        public List<string> AvailablePeriods { get; set; } = new();

        // 4 Thẻ thống kê (Kỳ được chọn)
        public PayrollSummaryCardDto Summary { get; set; } = new();

        // Biểu đồ cơ cấu thu nhập
        public IncomeStructureDto IncomeStructure { get; set; } = new();

        // Danh sách bảng lương có phân trang
        public List<EmployeePayrollItemDto> PayrollList { get; set; } = new();
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 5;
        public int TotalItems { get; set; } = 0;
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / (PageSize > 0 ? PageSize : 5));
        public int StartIndex => TotalItems == 0 ? 0 : (CurrentPage - 1) * PageSize + 1;
        public int EndIndex => Math.Min(CurrentPage * PageSize, TotalItems);

        // Lịch sử nhận lương (Timeline)
        public List<PaymentHistoryItemDto> PaymentHistory { get; set; } = new();
    }

    public class PayrollSummaryCardDto
    {
        public string PeriodLabel { get; set; } = string.Empty;
        public double NetSalary { get; set; }
        public double GrossSalary { get; set; }
        public double TotalDeduction { get; set; }
        public double ActualWorkDays { get; set; } = 22;
        public double StandardWorkDays { get; set; } = 22;
    }

    public class IncomeStructureDto
    {
        public string PeriodLabel { get; set; } = string.Empty;
        public double BasicSalary { get; set; }
        public double Allowances { get; set; }
        public double Bonus { get; set; }
        public double Other { get; set; }
        public double TotalGross { get; set; }

        public double BasicSalaryPercent => TotalGross > 0 ? Math.Round((BasicSalary / TotalGross) * 100, 1) : 0;
        public double AllowancesPercent => TotalGross > 0 ? Math.Round((Allowances / TotalGross) * 100, 1) : 0;
        public double BonusPercent => TotalGross > 0 ? Math.Round((Bonus / TotalGross) * 100, 1) : 0;
        public double OtherPercent => TotalGross > 0 ? Math.Max(0, Math.Round(100 - BasicSalaryPercent - AllowancesPercent - BonusPercent, 1)) : 0;
    }

    public class EmployeePayrollItemDto
    {
        public int Id { get; set; }
        public int Index { get; set; }
        public string Period { get; set; } = string.Empty;
        public double GrossSalary { get; set; }
        public double TotalDeduction { get; set; }
        public double NetSalary { get; set; }
        public double ActualWorkDays { get; set; } = 22;
        public double StandardWorkDays { get; set; } = 22;
        public PaymentStatus Status { get; set; } = PaymentStatus.DaThanhToan;
        public string StatusText { get; set; } = "Đã thanh toán";
        public DateTime? PaymentDate { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime CycleStartDate { get; set; }
        public DateTime CycleEndDate { get; set; }
    }

    public class PaymentHistoryItemDto
    {
        public int Id { get; set; }
        public string Period { get; set; } = string.Empty;
        public double NetSalary { get; set; }
        public DateTime? PaymentDate { get; set; }
        public DateTime? PaidAt { get; set; }
        public PaymentStatus Status { get; set; }
    }

    public class EmployeePayrollDetailDto
    {
        public int Id { get; set; }
        public string Period { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }

        // Ngày công
        public double ActualWorkDays { get; set; }
        public double StandardWorkDays { get; set; }
        public double HoursWorked { get; set; }

        // Thu nhập
        public double BasicSalary { get; set; }
        public double PositionAllowance { get; set; }
        public double OtherAllowances { get; set; }
        public double TotalAllowances { get; set; }
        public double PerformanceBonus { get; set; }
        public double OtherBonus { get; set; }
        public double TotalBonus { get; set; }
        public double OtherIncome { get; set; }
        public double TotalGrossSalary { get; set; }

        // Khấu trừ
        public double SocialInsurance { get; set; }
        public double HealthInsurance { get; set; }
        public double UnemploymentInsurance { get; set; }
        public double TotalInsurance { get; set; }
        public double PersonalIncomeTax { get; set; }
        public double OtherDeductions { get; set; }
        public double TotalDeductions { get; set; }

        // Thực lĩnh
        public double NetSalary { get; set; }

        // Trạng thái & thanh toán
        public PaymentStatus Status { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public DateTime? PaymentDate { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? PaymentNote { get; set; }
    }
}

