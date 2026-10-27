using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using giao_dien_demo.Models;

namespace giao_dien_demo.Controllers
{
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = AppPolicies.LeaderOnly)]
    public class LeaderAnalyticsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.ILeadershipScopeService _scopeService;
        private readonly giao_dien_demo.Services.IPayrollCalculationService _payrollService;

        public LeaderAnalyticsController(ApplicationDbContext context, giao_dien_demo.Services.ILeadershipScopeService scopeService, giao_dien_demo.Services.IPayrollCalculationService payrollService) 
        { 
            _context = context; 
            _scopeService = scopeService; 
            _payrollService = payrollService;
        }

        public async Task<IActionResult> Index(DateTime? from, DateTime? to, string? department)
        {
            var scope = _scopeService.GetOverseenOrganizationalUnits().Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().OrderBy(d => d).ToList();
            
            // Set default date range to last 6 months if not provided
            var maxCheckIn = await _context.Attendances.MaxAsync(a => (DateTime?)a.CheckIn) ?? DateTime.Today;
            var end = (to ?? new DateTime(maxCheckIn.Year, maxCheckIn.Month, DateTime.DaysInMonth(maxCheckIn.Year, maxCheckIn.Month))).Date;
            var start = (from ?? new DateTime(end.Year, end.Month, 1).AddMonths(-5)).Date;
            
            if (end < start) end = start;
            if (scope.Count == 0) return View(new LeaderAnalyticsViewModel { ScopeDepartments = scope, SelectedDepartment = department, From = start, To = end, HasSufficientData = false });
            
            var requested = string.IsNullOrWhiteSpace(department) ? null : department.Trim();
            if (requested != null && !scope.Contains(requested)) return Forbid();
            
            var targetDepts = requested != null ? new List<string> { requested } : scope;

            var employeesQuery = _context.Employees.AsNoTracking().Where(e => targetDepts.Contains(e.Department));
            var employees = await employeesQuery.Select(e => new { e.Id, e.Department, e.Status }).ToListAsync();
            var ids = employees.Select(e => e.Id).ToList();

            var attendance = await _context.Attendances.AsNoTracking().Where(a => a.EmployeeId.HasValue && ids.Contains(a.EmployeeId.Value) && a.CheckIn >= start && a.CheckIn < end.AddDays(1)).Select(a => new { a.EmployeeId, a.CheckIn, a.LateMinutes }).ToListAsync();
            var leaves = await _context.Leaves.AsNoTracking().Where(l => l.EmployeeId.HasValue && ids.Contains(l.EmployeeId.Value) && l.Status == "Đã duyệt" && l.FromDate < end.AddDays(1) && l.ToDate >= start).Select(l => new { l.EmployeeId, l.FromDate, l.ToDate }).ToListAsync();

            var departments = employees.Where(e => e.Status == "Active" || e.Status == "Probation").GroupBy(e => e.Department ?? "Chưa phân bổ").Select(g => new LeaderDepartmentSummary
            {
                Department = g.Key,
                EmployeeCount = g.Count(),
                AttendanceRecords = attendance.Count(a => a.EmployeeId.HasValue && g.Any(e => e.Id == a.EmployeeId.Value)),
                LateOccurrences = attendance.Count(a => a.EmployeeId.HasValue && a.LateMinutes > 0 && g.Any(e => e.Id == a.EmployeeId.Value)),
                ApprovedLeaveDays = leaves.Where(l => l.EmployeeId.HasValue && g.Any(e => e.Id == l.EmployeeId.Value)).Sum(l => Math.Max(1, (l.ToDate.Date - l.FromDate.Date).Days + 1))
            }).OrderBy(x => x.Department).ToList();

            // ==========================================
            // 1. CORRELATION LOGIC
            // ==========================================
            var correlations = new List<CorrelationResult>();
            
            // Calculate Leave Days and Late Occurrences per employee
            var employeeStats = ids.Select(id => new
            {
                EmployeeId = id,
                LateOccurrences = attendance.Count(a => a.EmployeeId == id && a.LateMinutes > 0),
                LeaveDays = leaves.Where(l => l.EmployeeId == id).Sum(l => Math.Max(1, (l.ToDate.Date - l.FromDate.Date).Days + 1))
            }).ToList();

            if (employeeStats.Count >= 3)
            {
                double corr = CalculatePearsonCorrelation(employeeStats.Select(e => (double)e.LateOccurrences).ToList(), employeeStats.Select(e => (double)e.LeaveDays).ToList());
                if (!double.IsNaN(corr))
                {
                    correlations.Add(new CorrelationResult
                    {
                        MetricA = "Số lần đi muộn",
                        MetricB = "Số ngày nghỉ phép",
                        PearsonCoefficient = corr,
                        Strength = GetCorrelationStrength(corr),
                        SampleSize = employeeStats.Count
                    });
                }
            }

            // ==========================================
            // 2. ANOMALY DETECTION (Z-SCORE)
            // ==========================================
            var anomalies = new List<AnomalyItem>();
            
            if (departments.Count >= 3)
            {
                var lateCounts = departments.Select(d => (double)d.LateOccurrences).ToList();
                double meanLate = lateCounts.Average();
                double stdLate = Math.Sqrt(lateCounts.Average(v => Math.Pow(v - meanLate, 2)));
                
                if (stdLate > 0)
                {
                    foreach (var dept in departments)
                    {
                        double zScore = (dept.LateOccurrences - meanLate) / stdLate;
                        if (Math.Abs(zScore) >= 1.5)
                        {
                            anomalies.Add(new AnomalyItem
                            {
                                Department = dept.Department,
                                Metric = "Đi muộn",
                                ActualValue = dept.LateOccurrences,
                                ExpectedValue = meanLate,
                                DifferencePercent = meanLate > 0 ? ((dept.LateOccurrences - meanLate) / meanLate) * 100 : 0,
                                ZScore = zScore,
                                Severity = Math.Abs(zScore) >= 2.5 ? "Bất thường mạnh" : "Cần chú ý"
                            });
                        }
                    }
                }
            }

            // ==========================================
            // 3. TREND FORECASTING (Linear Trend)
            // ==========================================
            var forecasts = new List<ForecastItem>();
            
            // Generate last 6 months data for the trend
            var historicalMonths = Enumerable.Range(0, 6).Select(i => new DateTime(end.Year, end.Month, 1).AddMonths(-5 + i)).ToList();
            var costData = new List<double>();
            
            foreach (var m in historicalMonths)
            {
                var calc = await _payrollService.CalculatePayrollAsync(m);
                var totalCost = calc.Where(s => s.EmployeeId.HasValue && ids.Contains(s.EmployeeId.Value)).Sum(s => s.TotalSalary);
                costData.Add(totalCost);
                
                forecasts.Add(new ForecastItem
                {
                    Month = m.ToString("MM/yyyy"),
                    ActualPersonnelCost = totalCost,
                    EstimatedPersonnelCost = null,
                    IsForecast = false
                });
            }

            // Forecast next 2 months if we have valid historical data
            if (costData.Any(c => c > 0) && costData.Count >= 3)
            {
                var y = costData.ToArray();
                var x = Enumerable.Range(1, y.Length).Select(i => (double)i).ToArray();
                
                double xMean = x.Average();
                double yMean = y.Average();
                
                double num = 0, den = 0;
                for (int i = 0; i < x.Length; i++)
                {
                    num += (x[i] - xMean) * (y[i] - yMean);
                    den += Math.Pow(x[i] - xMean, 2);
                }
                
                double slope = den == 0 ? 0 : num / den;
                double intercept = yMean - slope * xMean;
                
                for (int i = 1; i <= 2; i++)
                {
                    var forecastDate = historicalMonths.Last().AddMonths(i);
                    double estimatedCost = slope * (x.Length + i) + intercept;
                    if (estimatedCost < 0) estimatedCost = 0; // Cost cannot be negative
                    
                    forecasts.Add(new ForecastItem
                    {
                        Month = forecastDate.ToString("MM/yyyy"),
                        ActualPersonnelCost = 0,
                        EstimatedPersonnelCost = estimatedCost,
                        IsForecast = true
                    });
                }
            }

            return View(new LeaderAnalyticsViewModel 
            { 
                ScopeDepartments = scope, 
                SelectedDepartment = requested, 
                From = start, 
                To = end, 
                Departments = departments, 
                ScopedEmployeeCount = employees.Count,
                Correlations = correlations,
                Anomalies = anomalies,
                Forecasts = forecasts,
                HasSufficientData = (employeeStats.Count > 0 && costData.Any(c => c > 0))
            });
        }

        private double CalculatePearsonCorrelation(List<double> x, List<double> y)
        {
            if (x.Count != y.Count || x.Count == 0) return double.NaN;
            
            double sumX = x.Sum();
            double sumY = y.Sum();
            double sumX2 = x.Sum(v => v * v);
            double sumY2 = y.Sum(v => v * v);
            double sumXY = x.Zip(y, (a, b) => a * b).Sum();
            
            double n = x.Count;
            double num = sumXY - (sumX * sumY / n);
            double den = Math.Sqrt((sumX2 - sumX * sumX / n) * (sumY2 - sumY * sumY / n));
            
            if (den == 0) return double.NaN;
            return num / den;
        }

        private string GetCorrelationStrength(double corr)
        {
            double abs = Math.Abs(corr);
            if (abs >= 0.7) return "Mạnh";
            if (abs >= 0.4) return "Trung bình";
            return "Yếu";
        }
    }
}
