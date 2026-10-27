using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace giao_dien_demo.Models
{
    // ==========================================
    // 1. HR VIEWMODELS & INPUTS
    // ==========================================

    public class CreateKPIPeriodInput
    {
        [Required(ErrorMessage = "Vui lòng nhập tên kỳ đánh giá")]
        [StringLength(100, ErrorMessage = "Tên kỳ đánh giá không quá 100 ký tự")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc")]
        public DateTime EndDate { get; set; } = DateTime.Today.AddMonths(3);
    }

    public class KPIPeriodRowViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = string.Empty; // PLANNED, ACTIVE, CLOSED
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public int TotalEvaluations { get; set; }
        public int OutstandingDraft { get; set; }
        public int OutstandingActive { get; set; }
        public int OutstandingEvaluating { get; set; }
        public int CompletedEvaluations { get; set; }
    }

    public class KPIPeriodListViewModel
    {
        public List<KPIPeriodRowViewModel> Periods { get; set; } = new();
        public string StatusFilter { get; set; } = string.Empty;
        public string SearchTerm { get; set; } = string.Empty;
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
    }

    // ==========================================
    // 2. MANAGER VIEWMODELS & INPUTS
    // ==========================================

    public class ManagerKPIListViewModel
    {
        public List<ManagerKPIEvaluationRow> Items { get; set; } = new();
        public List<KPIPeriod> AvailablePeriods { get; set; } = new();
        public int? SelectedPeriodId { get; set; }
        public string StatusFilter { get; set; } = string.Empty;
        public string SearchTerm { get; set; } = string.Empty;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
        public int TotalScopedEmployees { get; set; }
        public int TotalEvaluations { get; set; }
        public int DraftEvaluations { get; set; }
        public int EvaluatingEvaluations { get; set; }
        public int CompletedEvaluations { get; set; }
    }

    public class ManagerKPIEvaluationRow
    {
        public int Id { get; set; }
        public int PeriodId { get; set; }
        public string PeriodName { get; set; } = string.Empty;
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int TargetCount { get; set; }
        public decimal ProgressAverage { get; set; }
        public decimal? FinalScore { get; set; }
        public string? FinalRating { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActionable { get; set; }
    }

    public class CreateEvaluationInput
    {
        [Required(ErrorMessage = "Vui lòng chọn kỳ đánh giá")]
        public int PeriodId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn nhân viên")]
        public int EmployeeId { get; set; }
    }

    public class CreateEvaluationViewModel
    {
        public List<KPIPeriod> Periods { get; set; } = new();
        public List<Employee> ScopedEmployees { get; set; } = new();
        public int? SelectedPeriodId { get; set; }
        public int? SelectedEmployeeId { get; set; }
    }

    public class ManagerEvaluationDetailViewModel
    {
        public KPIEvaluation Evaluation { get; set; } = null!;
        public List<KPITarget> Targets { get; set; } = new();
        public decimal TotalWeight { get; set; }
        public decimal ProgressAverage { get; set; }
        public decimal PreviewFinalScore { get; set; }
        public bool IsAllScored { get; set; }
        public List<KPIConfig> AvailableRatings { get; set; } = new();
        public bool InScope { get; set; }
        public bool IsActionable { get; set; }
    }

    public class AddTargetInput
    {
        [Required(ErrorMessage = "Vui lòng nhập tên mục tiêu")]
        [StringLength(200, ErrorMessage = "Tên mục tiêu không quá 200 ký tự")]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Mô tả không quá 1000 ký tự")]
        public string? Description { get; set; }

        [Range(0.01, 100, ErrorMessage = "Trọng số phải lớn hơn 0 và nhỏ hơn hoặc bằng 100%")]
        public decimal Weight { get; set; }
    }

    public class EditTargetInput
    {
        [Required]
        public int TargetId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên mục tiêu")]
        [StringLength(200, ErrorMessage = "Tên mục tiêu không quá 200 ký tự")]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Mô tả không quá 1000 ký tự")]
        public string? Description { get; set; }

        [Range(0.01, 100, ErrorMessage = "Trọng số phải lớn hơn 0 và nhỏ hơn hoặc bằng 100%")]
        public decimal Weight { get; set; }
    }

    public class TargetScoreInput
    {
        public int TargetId { get; set; }

        [Range(0, 100, ErrorMessage = "Điểm phải từ 0 đến 100")]
        public decimal Score { get; set; }
    }

    public class FinalizeEvaluationInput
    {
        [Required(ErrorMessage = "Vui lòng chọn xếp loại cuối cùng")]
        [StringLength(20)]
        public string FinalRating { get; set; } = string.Empty;
    }

    // ==========================================
    // 3. EMPLOYEE VIEWMODELS & INPUTS
    // ==========================================

    public class MyKPIListViewModel
    {
        public List<MyKPIEvaluationRow> Items { get; set; } = new();
        public List<KPIPeriod> AvailablePeriods { get; set; } = new();
        public int? SelectedPeriodId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
    }

    public class MyKPIEvaluationRow
    {
        public int Id { get; set; }
        public string PeriodName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public int TargetCount { get; set; }
        public decimal ProgressAverage { get; set; }
        public decimal? FinalScore { get; set; }
        public string? FinalRating { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class MyKPIDetailViewModel
    {
        public KPIEvaluation Evaluation { get; set; } = null!;
        public List<KPITarget> Targets { get; set; } = new();
        public decimal TotalWeight { get; set; }
        public decimal ProgressAverage { get; set; }
    }

    public class UpdateProgressInput
    {
        [Required]
        public int TargetId { get; set; }

        [Range(0, 100, ErrorMessage = "Tiến độ phải từ 0 đến 100%")]
        public decimal Progress { get; set; }
    }

    // ==========================================
    // 4. LEADER VIEWMODELS
    // ==========================================

    public class LeaderKPIViewModel
    {
        public string Search { get; set; } = string.Empty;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalEvaluations { get; set; }
        public List<string> AllowedDepts { get; set; } = new();
        public string CurrentDept { get; set; } = string.Empty;
        public List<KPIPeriod> Periods { get; set; } = new();
        public int? SelectedPeriodId { get; set; }

        // Analytics from COMPLETED evaluations
        public int CompletedEvaluationsCount { get; set; }
        public decimal AverageFinalScore { get; set; }
        public Dictionary<string, int> RatingDistribution { get; set; } = new();
        public List<LeaderCompletedEvaluationRow> CompletedEvaluations { get; set; } = new();

        // Current snapshot based on Employee.KPIScore (legacy compatibility)
        public List<Employee> CurrentEmployeeSnapshots { get; set; } = new();
    }

    public class LeaderCompletedEvaluationRow
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public string PeriodName { get; set; } = string.Empty;
        public decimal? FinalScore { get; set; }
        public string? FinalRating { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class LeaderKPIDetailViewModel
    {
        public KPIEvaluation Evaluation { get; set; } = null!;
        public List<KPITarget> Targets { get; set; } = new();
        public decimal TotalWeight { get; set; }
    }
}
