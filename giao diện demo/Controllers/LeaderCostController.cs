using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using System.Collections.Generic;

namespace giao_dien_demo.Controllers
{
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = giao_dien_demo.Authorization.AppPolicies.LeaderOnly)]
    public class LeaderCostController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.ILeadershipScopeService _scopeService;
        private readonly giao_dien_demo.Services.IPayrollCalculationService _payrollService;

        public LeaderCostController(ApplicationDbContext context, giao_dien_demo.Services.ILeadershipScopeService scopeService, giao_dien_demo.Services.IPayrollCalculationService payrollService)
        {
            _context = context;
            _scopeService = scopeService;
            _payrollService = payrollService;
        }

        public async Task<IActionResult> Index(int? year, int? month, string? department)
        {
            var allowedDepts = _scopeService.GetOverseenOrganizationalUnits();
            if (allowedDepts == null || allowedDepts.Count == 0)
            {
                ViewBag.Error = "Bạn không có quyền xem dữ liệu hoặc chưa được phân bổ đơn vị quản lý.";
                return View();
            }

            var minCheckIn = await _context.Attendances.MinAsync(a => (DateTime?)a.CheckIn) ?? DateTime.Now.AddMonths(-6);
            var maxCheckIn = await _context.Attendances.MaxAsync(a => (DateTime?)a.CheckIn) ?? DateTime.Now;
            var allPeriods = new List<DateTime>();
            for (var d = new DateTime(maxCheckIn.Year, maxCheckIn.Month, 1); d >= new DateTime(minCheckIn.Year, minCheckIn.Month, 1); d = d.AddMonths(-1))
            {
                allPeriods.Add(d);
            }
            
            DateTime selectedDate;
            if (year.HasValue && month.HasValue)
            {
                selectedDate = new DateTime(year.Value, month.Value, 1);
            }
            else
            {
                selectedDate = allPeriods.FirstOrDefault();
                if (selectedDate == default) selectedDate = DateTime.Now;
            }

            // Apply filters
            var targetDepts = allowedDepts;
            if (!string.IsNullOrEmpty(department) && allowedDepts.Contains(department))
            {
                targetDepts = new List<string> { department };
            }

            var scopedEmployees = await _scopeService.GetScopedEmployeeQuery().ToListAsync();
            
            // Tab 1: Tổng quỹ lương (Kỳ hiện tại)
            var currentCalculated = await _payrollService.CalculatePayrollAsync(selectedDate);
            var currentPeriodSalaries = currentCalculated
                .Where(s => s.EmployeeId.HasValue)
                .Join(scopedEmployees, s => s.EmployeeId.Value, e => e.Id, (s, e) => new { Salary = s, Employee = e })
                .Where(x => targetDepts.Contains(x.Employee.Department))
                .ToList();

            var previousDate = selectedDate.AddMonths(-1);
            var previousCalculated = await _payrollService.CalculatePayrollAsync(previousDate);
            var previousPeriodSalaries = previousCalculated
                .Where(s => s.EmployeeId.HasValue)
                .Join(scopedEmployees, s => s.EmployeeId.Value, e => e.Id, (s, e) => new { Salary = s, Employee = e })
                .Where(x => targetDepts.Contains(x.Employee.Department))
                .ToList();

            var currentTotalGross = currentPeriodSalaries.Sum(x => x.Salary.TotalSalary);
            var currentHeadcount = currentPeriodSalaries.Count;
            var previousTotalGross = previousPeriodSalaries.Sum(x => x.Salary.TotalSalary);
            var previousHeadcount = previousPeriodSalaries.Count;

            ViewBag.CurrentTotalGross = currentTotalGross;
            ViewBag.CurrentHeadcount = currentHeadcount;
            ViewBag.CurrentAverage = currentHeadcount > 0 ? currentTotalGross / currentHeadcount : 0;
            
            ViewBag.PreviousTotalGross = previousTotalGross;
            ViewBag.PreviousAverage = previousHeadcount > 0 ? previousTotalGross / previousHeadcount : 0;
            
            // Tab 2: Chi phí theo phòng ban (Current period)
            var byDepartment = currentPeriodSalaries
                .GroupBy(x => x.Employee.Department)
                .Select(g => new 
                {
                    Department = g.Key ?? "Chưa phân bổ",
                    Headcount = g.Count(),
                    TotalGross = g.Sum(x => x.Salary.TotalSalary),
                    Average = g.Count() > 0 ? g.Sum(x => x.Salary.TotalSalary) / g.Count() : 0,
                    Percentage = currentTotalGross > 0 ? (g.Sum(x => x.Salary.TotalSalary) / currentTotalGross) * 100 : 0
                })
                .OrderByDescending(x => x.TotalGross)
                .ToList();
            
            ViewBag.ByDepartment = byDepartment;

            // Tab 3: Xu hướng chi phí (Last 6 months for the selected filter)
            var last6Months = System.Linq.Enumerable.Range(0, 6).Select(i => selectedDate.AddMonths(-i)).Reverse().ToList();
            var trendData = new List<dynamic>();

            foreach (var d in last6Months)
            {
                var mCalc = await _payrollService.CalculatePayrollAsync(d);
                var mTotal = mCalc
                    .Where(s => s.EmployeeId.HasValue)
                    .Join(scopedEmployees, s => s.EmployeeId.Value, e => e.Id, (s, e) => new { Salary = s, Employee = e })
                    .Where(x => targetDepts.Contains(x.Employee.Department))
                    .Sum(x => x.Salary.TotalSalary);
                
                trendData.Add(new 
                {
                    Month = d.ToString("MM/yyyy"),
                    TotalGross = mTotal
                });
            }

            ViewBag.TrendData = trendData;

            ViewBag.SelectedYear = selectedDate.Year;
            ViewBag.SelectedMonth = selectedDate.Month;
            ViewBag.SelectedDepartment = department;
            ViewBag.AllowedDepartments = allowedDepts;
            ViewBag.AvailablePeriods = allPeriods;

            return View();
        }
    }
}
