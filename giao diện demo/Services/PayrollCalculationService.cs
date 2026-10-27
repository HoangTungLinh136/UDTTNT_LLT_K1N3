using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Helpers;

namespace giao_dien_demo.Services
{
    public interface IPayrollCalculationService
    {
        Task<List<Salary>> CalculatePayrollAsync(DateTime targetDate);
    }

    public class PayrollCalculationService : IPayrollCalculationService
    {
        private readonly ApplicationDbContext _context;

        public PayrollCalculationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Salary>> CalculatePayrollAsync(DateTime targetDate)
        {
            var result = new List<Salary>();

            var currentCycle = PayrollCycleHelper.GetCurrentCycle(targetDate);
            var settings = PayrollSettingService.GetAllVersions().FirstOrDefault(x => x.Status == "ACTIVE");
            var components = PayrollComponentService.GetActiveComponents();

            // Lấy toàn bộ chấm công đã xác nhận trong kỳ
            var allAttendance = await _context.Attendances
                .Where(x => x.CheckOut.HasValue && x.CheckIn >= currentCycle.StartDate && x.CheckIn <= currentCycle.EndDate)
                .ToListAsync();

            // P40: Lấy tất cả nghỉ phép đã duyệt trong kỳ lương để bù giờ chuẩn
            var approvedLeaves = await _context.Leaves
                .Where(l => l.Status == "Đã duyệt" &&
                            l.FromDate <= currentCycle.EndDate &&
                            l.ToDate >= currentCycle.StartDate)
                .ToListAsync();

            // Lấy danh sách nhân viên
            var allEmployees = await _context.Employees.ToListAsync();

            var salaryGradeConfigs = await _context.SalaryGradeConfigs.ToListAsync();
            var jobTitleConfigs = await _context.JobTitleConfigs.ToListAsync();
            var experienceConfigs = await _context.ExperienceConfigs.ToListAsync();
            var kpiConfigs = await _context.KPIConfigs.ToListAsync();
            var rewardRecords = await _context.RewardRecords
                .Where(r => r.ApprovalStatus == "APPROVED" && r.HasMonetaryReward && r.PayrollPeriod == currentCycle.PeriodName)
                .ToListAsync();

            foreach (var emp in allEmployees)
            {
                var atts = allAttendance.Where(a => a.EmployeeId == emp.Id).ToList();
                
                // Thu thập Inputs
                double luongCoBan = (double)emp.Salary;
                double stdHours = atts.Where(x => x.CheckIn.DayOfWeek != DayOfWeek.Sunday).Sum(x => x.StandardHours);
                double normalOtHours = atts.Where(x => x.CheckIn.DayOfWeek != DayOfWeek.Sunday).Sum(x => x.OvertimeHours);
                double weekendOtHours = atts.Where(x => x.CheckIn.DayOfWeek == DayOfWeek.Sunday).Sum(x => x.OvertimeHours);

                // P40 FIX: Bù giờ chuẩn cho ngày nghỉ phép có lương mà nhân viên không chấm công
                var empLeaves = approvedLeaves.Where(l => l.EmployeeId == emp.Id).ToList();

                foreach (var leave in empLeaves)
                {
                    // Chỉ tính ngày nghỉ có lương (loại trừ Nghỉ không lương)
                    if (leave.LeaveType == "Nghỉ không lương") continue;

                    var leaveStart = leave.FromDate < currentCycle.StartDate ? currentCycle.StartDate : leave.FromDate;
                    var leaveEnd = leave.ToDate > currentCycle.EndDate ? currentCycle.EndDate : leave.ToDate;

                    for (var day = leaveStart.Date; day <= leaveEnd.Date; day = day.AddDays(1))
                    {
                        // Bỏ qua Chủ nhật
                        if (day.DayOfWeek == DayOfWeek.Sunday) continue;

                        // Không bù nếu đã có bản ghi chấm công ngày đó (tránh tính trùng)
                        bool hasAttendance = atts.Any(a => a.CheckIn.Date == day);
                        if (hasAttendance) continue;

                        stdHours += 8.0;
                    }
                }

                double workDays = stdHours / 8.0;

                // Các khoản phụ cấp và thưởng cơ bản
                double phuCapChucVu = (double)emp.PositionAllowance;
                double phuCapDocHai = (double)emp.HazardousAllowance;
                double heSoVung = (double)emp.RegionalAllowanceSystem;
                double phuCapKhuVuc = luongCoBan * heSoVung;
                double phuCapBangCap = (double)emp.QualificationAllowance;

                double holidayBonus = 0;
                double reward = rewardRecords.Where(r => r.EmployeeId == emp.Id).Sum(r => (double)r.RewardAmount);

                // Hệ số hiệu suất
                decimal hsBangCap = salaryGradeConfigs.FirstOrDefault(x => x.AcademicLevel == emp.AcademicLevel)?.Coefficient ?? 1.0m;
                decimal hsViTri = jobTitleConfigs.FirstOrDefault(x => x.JobTitle == emp.JobTitle)?.Coefficient ?? 1.0m;
                decimal hsKinhNghiem = experienceConfigs.FirstOrDefault(x => x.MinYears <= emp.ExperienceYears && emp.ExperienceYears <= x.MaxYears)?.Coefficient ?? 1.0m;
                decimal hsKPI = kpiConfigs.FirstOrDefault(x => x.KPIRating == emp.KPIScore)?.Coefficient ?? 1.0m;

                double performanceMult = (double)(hsBangCap * hsViTri * hsKinhNghiem * hsKPI);

                // Sử dụng DataTable Compute cho công thức động (Simplified example of formula evaluation)
                // Trong thực tế, các biến sẽ được replace trong chuỗi Formula trước khi Compute.
                // Ở đây mô phỏng tính toán động cho BASE_SALARY và OT_HOURS nếu có.
                
                double computedBaseSalary = (luongCoBan / 208.0) * stdHours * performanceMult;
                double computedOtSalary = ((luongCoBan / 208.0) * normalOtHours * 1.5) + ((luongCoBan / 208.0) * weekendOtHours * 2.0);

                double totalBonusAllowance = phuCapChucVu + phuCapDocHai + phuCapKhuVuc + phuCapBangCap + computedOtSalary;

                double totalSalary = computedBaseSalary + totalBonusAllowance + holidayBonus + reward;

                // Tính Bảo hiểm (Mock: 10.5% của Lương cơ bản)
                double bhxh = luongCoBan * 0.08;
                double bhyt = luongCoBan * 0.015;
                double bhtn = luongCoBan * 0.01;
                double totalInsurance = bhxh + bhyt + bhtn;

                // Tính Thuế (Mock: Cơ bản)
                int dependents = 0; // Thay bằng giá trị thực tế nếu có trong model
                double giamTruBanThan = 11000000;
                double giamTruNguoiPhuThuoc = dependents * 4400000;
                double thuNhapTinhThue = totalSalary - totalInsurance - giamTruBanThan - giamTruNguoiPhuThuoc;
                if (thuNhapTinhThue < 0) thuNhapTinhThue = 0;

                double tax = 0;
                if (thuNhapTinhThue <= 5000000) tax = thuNhapTinhThue * 0.05;
                else if (thuNhapTinhThue <= 10000000) tax = (5000000 * 0.05) + ((thuNhapTinhThue - 5000000) * 0.10);
                else tax = thuNhapTinhThue * 0.10; // Giản lược lũy tiến

                double netSalary = totalSalary - totalInsurance - tax;

                var salaryRecord = new Salary
                {
                    EmployeeId = emp.Id,
                    EmployeeNameSnapshot = emp.Name,
                    BasicSalary = luongCoBan,
                    HoursWorked = stdHours + normalOtHours + weekendOtHours,
                    BonusAndAllowance = totalBonusAllowance,
                    AllowanceCost = phuCapChucVu + phuCapDocHai + phuCapKhuVuc + phuCapBangCap,
                    OvertimeCost = computedOtSalary,
                    PerformanceSalary = computedBaseSalary,
                    NumberOfDependents = dependents,
                    HolidayBonus = holidayBonus,
                    DiligenceBonus = 0,
                    Reward = reward,
                    Discipline = 0,
                    TotalSalary = totalSalary,
                    Insurance = totalInsurance,
                    TaxTNCN = tax,
                    NetSalary = netSalary,
                    TaxableIncome = thuNhapTinhThue,
                    TotalDeduction = totalInsurance + tax,
                    TaxCode = emp.CitizenId,
                    SalaryMonth = currentCycle.StartDate,
                    DepartmentName = emp.Department
                };

                result.Add(salaryRecord);
            }

            return result;
        }
    }
}
