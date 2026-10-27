using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Authorization;
using System.Security.Claims;

namespace giao_dien_demo.Services
{
    public class AiContextBuilderService
    {
        private readonly ApplicationDbContext _context;
        private readonly IManagerScopeService _managerScope;
        private readonly ILeadershipScopeService _leaderScope;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AiContextBuilderService(ApplicationDbContext context, IManagerScopeService managerScope, ILeadershipScopeService leaderScope, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _managerScope = managerScope;
            _leaderScope = leaderScope;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<string> BuildContextAsync(string prompt)
        {
            var principal = _httpContextAccessor.HttpContext?.User;
            var role = principal?.FindFirstValue(ClaimTypes.Role) ?? AppRoles.Employee;
            var sb = new StringBuilder();
            sb.AppendLine("=== DỮ LIỆU HỆ THỐNG CUNG CẤP TỰ ĐỘNG ===");
            var lowerPrompt = prompt.ToLowerInvariant();

            var username = principal == null ? null : CurrentActor.Username(principal);
            var currentUser = string.IsNullOrWhiteSpace(username) ? null : await _context.Users
                .Include(u => u.Employee)
                .SingleOrDefaultAsync(u => u.Username == username && u.CanLogin);

            var currentEmployee = currentUser?.Employee;

            if (currentEmployee != null)
            {
                sb.AppendLine($"Tên người dùng hiện tại: {currentEmployee.Name}");
                sb.AppendLine($"Phòng ban: {currentEmployee.Department ?? "Không rõ"}, Vị trí: {currentEmployee.JobTitle ?? "Không rõ"}");
                sb.AppendLine($"Vai trò trên hệ thống: {role}");
            }

            // ----------------------------------------------------
            // ADMIN SCOPE
            // ----------------------------------------------------
            if (role == AppRoles.Admin)
            {
                sb.AppendLine("\n--- Dữ liệu Quản trị Hệ thống ---");
                
                if (lowerPrompt.Contains("lỗi") || lowerPrompt.Contains("error"))
                {
                    var errors = await _context.SystemErrorLogs.OrderByDescending(e => e.Timestamp).Take(5).ToListAsync();
                    sb.AppendLine("5 lỗi hệ thống gần nhất:");
                    foreach(var e in errors) sb.AppendLine($"- {e.Timestamp}: {e.Message}");
                }
                if (lowerPrompt.Contains("cấu hình") || lowerPrompt.Contains("config"))
                {
                    string[] allowedConfigKeys = { "CompanyName", "TimeZone", "DateFormat", "Language" };
                    var configs = await _context.SystemConfigs
                        .Where(c => allowedConfigKeys.Contains(c.Key))
                        .ToListAsync();
                    sb.AppendLine("Cấu hình hệ thống hiện tại:");
                    foreach(var c in configs) sb.AppendLine($"- {c.Key}: {c.Value}");
                }
                if (lowerPrompt.Contains("nhân viên") || lowerPrompt.Contains("lương") || lowerPrompt.Contains("phép"))
                {
                    sb.AppendLine("[HỆ THỐNG CHẶN]: Admin không có quyền truy cập trực tiếp dữ liệu nhân sự (Lương, Phép, v.v.).");
                }
                sb.AppendLine("=== KẾT THÚC DỮ LIỆU ===");
                return sb.ToString();
            }

            if (currentUser?.EmployeeId == null || currentEmployee == null)
            {
                sb.AppendLine("[HỆ THỐNG CHẶN]: Không xác định được hồ sơ nhân viên của tài khoản hiện tại.");
                sb.AppendLine("=== KẾT THÚC DỮ LIỆU ===");
                return sb.ToString();
            }

            // ----------------------------------------------------
            // HR SCOPE
            // ----------------------------------------------------
            if (role == AppRoles.HR)
            {
                sb.AppendLine("\n--- Dữ liệu Nhân sự Toàn hệ thống (HR Scope) ---");
                if (lowerPrompt.Contains("nhân viên") || lowerPrompt.Contains("tổng"))
                {
                    var totalEmp = await _context.Employees.CountAsync();
                    sb.AppendLine($"Tổng số nhân viên: {totalEmp}");
                }
                if (lowerPrompt.Contains("nghỉ phép") || lowerPrompt.Contains("đơn"))
                {
                    var pendingLeaves = await _context.Leaves.CountAsync(l => l.Status == "Chờ duyệt");
                    sb.AppendLine($"Tổng số đơn nghỉ phép đang chờ duyệt: {pendingLeaves}");
                }
                sb.AppendLine("=== KẾT THÚC DỮ LIỆU ===");
                return sb.ToString();
            }

            // ----------------------------------------------------
            // LEADER SCOPE
            // ----------------------------------------------------
            if (role == AppRoles.Leader)
            {
                sb.AppendLine("\n--- Dữ liệu Tổng hợp Ban Lãnh đạo (Leader Scope) ---");
                var scopedEmps = _leaderScope.GetScopedEmployeeQuery();
                
                if (lowerPrompt.Contains("nhân viên") || lowerPrompt.Contains("tổng"))
                {
                    var totalEmp = await scopedEmps.CountAsync();
                    sb.AppendLine($"Tổng số nhân viên trong phạm vi quản lý: {totalEmp}");
                }
                if (lowerPrompt.Contains("lương"))
                {
                    var empIds = await scopedEmps.Select(e => e.Id).ToListAsync();
                    var recentPayrollSum = await _context.PayrollPayments
                        .Where(p => empIds.Contains(p.EmployeeId))
                        .OrderByDescending(p => p.CreatedAt)
                        .Take(50)
                        .SumAsync(p => p.NetSalary);
                    sb.AppendLine($"Tổng chi phí lương gần đây (ước tính trong scope): {recentPayrollSum:N0} VNĐ");
                }
                sb.AppendLine("=== KẾT THÚC DỮ LIỆU ===");
                return sb.ToString();
            }

            // ----------------------------------------------------
            // MANAGER & EMPLOYEE SCOPE
            // ----------------------------------------------------
            
            // Common Personal Data
            if (currentEmployee != null)
            {
                if (lowerPrompt.Contains("chấm công") || lowerPrompt.Contains("đi làm"))
                {
                    var today = DateTime.Today;
                    var monthStart = new DateTime(today.Year, today.Month, 1);
                    var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                    var attendances = await _context.Attendances
                        .Where(a => a.EmployeeId == currentEmployee.Id && a.CheckIn >= monthStart && a.CheckIn <= monthEnd)
                        .OrderByDescending(a => a.CheckIn)
                        .Take(10)
                        .ToListAsync();

                    sb.AppendLine("\n--- Dữ liệu cá nhân: Chấm công (10 ngày gần nhất) ---");
                    foreach (var a in attendances) sb.AppendLine($"- Ngày {a.CheckIn:dd/MM/yyyy}: CheckIn: {a.CheckIn}, CheckOut: {a.CheckOut}, Trạng thái: {a.StatusNote}");
                }

                if (lowerPrompt.Contains("nghỉ phép") || lowerPrompt.Contains("xin nghỉ"))
                {
                    var leaves = await _context.Leaves
                        .Where(l => l.EmployeeId == currentEmployee.Id)
                        .OrderByDescending(l => l.CreatedDate)
                        .Take(5)
                        .ToListAsync();

                    sb.AppendLine("\n--- Dữ liệu cá nhân: Nghỉ phép (5 đơn gần nhất) ---");
                    foreach (var l in leaves) sb.AppendLine($"- Từ {l.FromDate:dd/MM/yyyy} đến {l.ToDate:dd/MM/yyyy}: Lý do: {l.Reason}, Trạng thái: {l.Status}");
                }

                if (lowerPrompt.Contains("lương"))
                {
                    var payrolls = await _context.PayrollPayments
                        .Where(p => p.EmployeeId == currentEmployee.Id)
                        .OrderByDescending(p => p.CreatedAt)
                        .Take(3)
                        .ToListAsync();

                    sb.AppendLine("\n--- Dữ liệu cá nhân: Lương (3 kỳ gần nhất) ---");
                    foreach (var p in payrolls) sb.AppendLine($"- Kỳ {p.PayrollPeriod}: Thực nhận: {p.NetSalary:N0} VNĐ, Trạng thái: {p.Status}");
                }
            }

            // Manager specific
            if (role == AppRoles.Manager && (lowerPrompt.Contains("nhân viên") || lowerPrompt.Contains("đội nhóm")))
            {
                var subordinates = await _managerScope.GetScopedEmployeeQuery()
                    .Take(5)
                    .Select(e => new { e.Name, e.Department, e.JobTitle })
                    .ToListAsync();

                if (subordinates.Any())
                {
                    sb.AppendLine("\n--- Dữ liệu Đội nhóm (Manager Scope) ---");
                    foreach (var e in subordinates)
                    {
                        sb.AppendLine($"- {e.Name} ({e.JobTitle ?? "Không có chức vụ"} - {e.Department ?? "Không có phòng ban"})");
                    }
                }
                else
                {
                    sb.AppendLine("\n--- Dữ liệu Đội nhóm ---");
                    sb.AppendLine("MANAGER BUSINESS DATA NOT YET AVAILABLE.");
                }
            }

            sb.AppendLine("=== KẾT THÚC DỮ LIỆU ===");

            return sb.ToString();
        }
    }
}
