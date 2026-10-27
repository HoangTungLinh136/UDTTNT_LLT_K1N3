using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;

namespace giao_dien_demo.Helpers
{
    public class PayrollValidationService
    {
        public static async Task<PayrollValidationSummaryDto> RunValidationAsync(ApplicationDbContext context, DateTime? targetDate = null)
        {
            var summary = new PayrollValidationSummaryDto();
            var issues = new List<PayrollValidationIssueDto>();
            
            var cycle = PayrollCycleHelper.GetCurrentCycle(targetDate ?? DateTime.Now);
            var employees = await context.Employees.ToListAsync();
            
            // Lấy tất cả attendance trong kỳ (chỉ lấy cho những ngày <= hôm nay nếu đang trong tháng)
            var maxDate = DateTime.Now < cycle.EndDate ? DateTime.Now.Date : cycle.EndDate.Date;
            var attendances = await context.Attendances
                .Where(a => a.CheckIn.Date >= cycle.StartDate && a.CheckIn.Date <= maxDate)
                .ToListAsync();

            foreach (var emp in employees)
            {
                // RULE 1: Lương cơ bản <= 0 (BLOCKING)
                if (emp.Salary <= 0)
                {
                    issues.Add(new PayrollValidationIssueDto
                    {
                        Severity = "ERROR",
                        EmployeeName = emp.Name,
                        EmployeeCode = emp.EmployeeCode,
                        Message = "Mức lương cơ bản đang bằng 0 hoặc chưa được thiết lập.",
                        SourceModule = "Hồ sơ nhân viên",
                        Component = "Mức lương cơ bản (Salary)",
                        CurrentValue = emp.Salary.ToString("N0") + " ₫",
                        ExpectedValue = "> 0 ₫",
                        ResolutionHint = "Vui lòng cập nhật Mức lương cơ bản hợp lệ trong hồ sơ của nhân viên.",
                        TargetUrl = $"/Employee/Edit/{emp.Id}"
                    });
                }

                // RULE 2: Trống CitizenId (MST) (WARNING)
                if (string.IsNullOrEmpty(emp.CitizenId))
                {
                    issues.Add(new PayrollValidationIssueDto
                    {
                        Severity = "WARNING",
                        EmployeeName = emp.Name,
                        EmployeeCode = emp.EmployeeCode,
                        Message = "Thiếu thông tin Mã số thuế (CCCD) để khấu trừ thuế.",
                        SourceModule = "Hồ sơ nhân viên",
                        Component = "Mã số thuế / CCCD",
                        CurrentValue = "Trống",
                        ExpectedValue = "Có dữ liệu",
                        ResolutionHint = "Vui lòng bổ sung số CCCD/Mã số thuế cho nhân viên này.",
                        TargetUrl = $"/Employee/Edit/{emp.Id}"
                    });
                }

                // RULE 3: Trống Bank Account (WARNING)
                if (string.IsNullOrEmpty(emp.BankAccountNumber))
                {
                    issues.Add(new PayrollValidationIssueDto
                    {
                        Severity = "WARNING",
                        EmployeeName = emp.Name,
                        EmployeeCode = emp.EmployeeCode,
                        Message = "Chưa có thông tin số tài khoản ngân hàng để chi trả lương.",
                        SourceModule = "Hồ sơ nhân viên",
                        Component = "Tài khoản ngân hàng",
                        CurrentValue = "Chưa có",
                        ExpectedValue = "Có dữ liệu hợp lệ",
                        ResolutionHint = "Để thực hiện chi trả lương chuyển khoản, vui lòng cập nhật số TK.",
                        TargetUrl = $"/Employee/Edit/{emp.Id}"
                    });
                }

                // RULE 4: Quên Check-out (BLOCKING)
                var empAttendances = attendances.Where(a => a.EmployeeId == emp.Id).ToList();
                var missingCheckout = empAttendances.Where(a => !a.CheckOut.HasValue).ToList();
                
                foreach (var att in missingCheckout)
                {
                    issues.Add(new PayrollValidationIssueDto
                    {
                        Severity = "ERROR",
                        EmployeeName = emp.Name,
                        EmployeeCode = emp.EmployeeCode,
                        Message = $"Quên Check-out ngày {att.CheckIn:dd/MM/yyyy}. Cần bổ sung để tính giờ công.",
                        SourceModule = "Chấm công",
                        Component = "Giờ ra (Check-Out)",
                        CurrentValue = "Null",
                        ExpectedValue = "Giờ hợp lệ",
                        ResolutionHint = $"Sử dụng tính năng Chỉnh sửa chấm công hoặc Chốt công ngày {att.CheckIn:dd/MM/yyyy}.",
                        TargetUrl = $"/Timesheet/Index" // Route tới bảng công
                    });
                }
            }

            // Tổng kết
            summary.Issues = issues;
            summary.ErrorCount = issues.Count(i => i.Severity == "ERROR");
            summary.WarningCount = issues.Count(i => i.Severity == "WARNING");
            
            // Ready nếu không có ERROR nào
            summary.IsReadyForPayroll = summary.ErrorCount == 0;

            return summary;
        }
    }
}

