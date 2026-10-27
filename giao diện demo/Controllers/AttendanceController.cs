using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Models;
using giao_dien_demo.Data; // Chứa ApplicationDbContext
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using giao_dien_demo.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize]
    public class AttendanceController : Controller
    {
        // 🔥 Đã xóa List tĩnh. Bây giờ dùng Database qua _context
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<DashboardHub> _hubContext;
        private readonly giao_dien_demo.Services.IAttendanceCalculationService _attendanceCalc;
        private readonly giao_dien_demo.Services.IAuditLogService _auditLogService;
        private readonly giao_dien_demo.Services.IInAppNotificationService _notifications;
        private readonly giao_dien_demo.Services.IManagerScopeService _managerScope;

        public AttendanceController(ApplicationDbContext context, IHubContext<DashboardHub> hubContext,
            giao_dien_demo.Services.IAttendanceCalculationService attendanceCalc,
            giao_dien_demo.Services.IAuditLogService auditLogService,
            giao_dien_demo.Services.IInAppNotificationService notifications,
            giao_dien_demo.Services.IManagerScopeService managerScope)
        {
            _context = context;
            _hubContext = hubContext;
            _attendanceCalc = attendanceCalc;
            _auditLogService = auditLogService;
            _notifications = notifications;
            _managerScope = managerScope;
        }

        private bool CheckLogin()
        {
            return User?.Identity?.IsAuthenticated == true;
        }

        private async Task<Employee?> GetCurrentEmployee()
        {
            var user = await CurrentActor.ResolveUserAsync(User, _context);
            return user?.EmployeeId == null ? null : await _context.Employees.FindAsync(user.EmployeeId.Value);
        }

        private bool IsCurrentManager()
        {
            return (CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role")) == AppRoles.Manager;
        }

        private IQueryable<Attendance> ApplyManagerScope(IQueryable<Attendance> query)
        {
            if (!IsCurrentManager()) return query;
            var scopedEmployeeIds = _managerScope.GetScopedEmployeeQuery().Select(e => e.Id);
            return query.Where(a => a.EmployeeId.HasValue && scopedEmployeeIds.Contains(a.EmployeeId.Value));
        }

        // --- 1. HIỂN THỊ: Lấy dữ liệu từ SQL Server (HR LIVE ATTENDANCE DASHBOARD) ---
        public async Task<IActionResult> Index(DateTime? date, string? department, string? status, string? searchTerm, int? requestId = null, int page = 1)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");

            var userRole = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            var realName = HttpContext.Session.GetString("RealName");
            
            if (userRole == "Employee")
            {
                return View("MyIndex");
            }

            ViewBag.Role = userRole;
            ViewBag.RealName = realName;

            DateTime selectedDate = date ?? DateTime.Today;
            int pageSize = 50;
            int pageNumber = page < 1 ? 1 : page;

            var summary = await CalculateLiveSummaryAsync(selectedDate);
            var (rows, totalItems) = await QueryLiveAttendanceRowsAsync(selectedDate, department, status, searchTerm, pageNumber, pageSize);
            var departments = await _context.Employees
                .Where(e => !string.IsNullOrEmpty(e.Department))
                .Select(e => e.Department!)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            var vm = new HRLiveAttendanceViewModel
            {
                Summary = summary,
                Rows = rows,
                Departments = departments,
                SelectedDate = selectedDate,
                SelectedDepartment = department,
                SelectedStatus = status,
                SearchTerm = searchTerm,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            var requests = new List<AttendanceAdjustmentRequest>();
            if (userRole == "HR" || userRole == "Leader" || userRole == "Manager")
            {
                var requestQuery = _context.AttendanceAdjustmentRequests.AsQueryable();
                if (userRole == AppRoles.Manager)
                {
                    var scopedCodes = await _managerScope.GetScopedEmployeeQuery()
                        .Where(e => e.EmployeeCode != null)
                        .Select(e => e.EmployeeCode!)
                        .ToListAsync();
                    requestQuery = requestQuery.Where(r => scopedCodes.Contains(r.EmployeeCode));
                }
                requests = await requestQuery.OrderByDescending(r => r.CreatedAt).Take(20).ToListAsync();
            }
            ViewBag.Requests = requests;
            ViewBag.RequestId = requestId;

            return View(vm);
        }

        private async Task<HRLiveSummaryDto> CalculateLiveSummaryAsync(DateTime targetDate)
        {
            var date = targetDate.Date;
            var employeeQuery = IsCurrentManager()
                ? _managerScope.GetScopedEmployeeQuery()
                : _context.Employees.AsQueryable();
            var totalEmployees = await employeeQuery.CountAsync(e => e.Status == "Active");
            
            var todayAttendances = await ApplyManagerScope(_context.Attendances
                .Where(a => a.CheckIn.Date == date))
                .ToListAsync();

            var checkedInEmpIds = todayAttendances
                .Where(a => a.EmployeeId.HasValue)
                .Select(a => a.EmployeeId!.Value)
                .Distinct()
                .ToHashSet();

            var workingEmpIds = todayAttendances
                .Where(a => a.CheckOut == null && a.EmployeeId.HasValue)
                .Select(a => a.EmployeeId!.Value)
                .Distinct()
                .ToHashSet();

            var checkedInCount = checkedInEmpIds.Count;
            var currentlyWorkingCount = workingEmpIds.Count;
            var lateCount = todayAttendances
                .Where(a => a.LateMinutes > 0 && a.EmployeeId.HasValue)
                .Select(a => a.EmployeeId!.Value)
                .Distinct()
                .Count();

            var checkedOutCount = todayAttendances
                .Where(a => a.CheckOut != null && a.EmployeeId.HasValue && !workingEmpIds.Contains(a.EmployeeId.Value))
                .Select(a => a.EmployeeId!.Value)
                .Distinct()
                .Count();

            var notCheckedInCount = Math.Max(0, totalEmployees - checkedInCount);

            return new HRLiveSummaryDto
            {
                TotalEmployees = totalEmployees,
                CheckedInCount = checkedInCount,
                CurrentlyWorkingCount = currentlyWorkingCount,
                LateCount = lateCount,
                CheckedOutCount = checkedOutCount,
                NotCheckedInCount = notCheckedInCount,
                ServerTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
            };
        }

        private async Task<(List<HRLiveAttendanceRowDto> Rows, int TotalItems)> QueryLiveAttendanceRowsAsync(
            DateTime targetDate, string? department, string? status, string? searchTerm, int page, int pageSize)
        {
            var date = targetDate.Date;
            var employeeQuery = IsCurrentManager()
                ? _managerScope.GetScopedEmployeeQuery()
                : _context.Employees.AsQueryable();
            var employees = await employeeQuery.AsNoTracking().ToListAsync();
            var empDict = employees.ToDictionary(e => e.Id, e => e);

            if (string.Equals(status, "NOT_CHECKED_IN", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "Chưa chấm công", StringComparison.OrdinalIgnoreCase))
            {
                var checkedInEmpIds = await _context.Attendances
                    .Where(a => a.CheckIn.Date == date && a.EmployeeId.HasValue)
                    .Select(a => a.EmployeeId!.Value)
                    .Distinct()
                    .ToListAsync();

                var notCheckedInQuery = employees.Where(e => !checkedInEmpIds.Contains(e.Id));

                if (!string.IsNullOrWhiteSpace(department) && department != "Tất cả")
                {
                    notCheckedInQuery = notCheckedInQuery.Where(e => string.Equals(e.Department, department, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var term = searchTerm.Trim();
                    notCheckedInQuery = notCheckedInQuery.Where(e => 
                        (e.Name != null && e.Name.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                        (e.EmployeeCode != null && e.EmployeeCode.Contains(term, StringComparison.OrdinalIgnoreCase)));
                }

                var notCheckedList = notCheckedInQuery.ToList();
                var total = notCheckedList.Count;
                var paged = notCheckedList.Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(e => new HRLiveAttendanceRowDto
                    {
                        AttendanceId = 0,
                        EmployeeId = e.Id,
                        EmployeeCode = e.EmployeeCode ?? "",
                        EmployeeName = e.Name ?? "Nhân viên",
                        DepartmentName = e.Department ?? "",
                        AvatarUrl = e.AvatarPath,
                        CheckIn = null,
                        CheckOut = null,
                        StatusCode = "NOT_CHECKED_IN",
                        StatusText = "Chưa chấm công",
                        LateMinutes = 0,
                        WorkingHours = 0,
                        StatusNote = "Chưa có lượt chấm công trong ngày",
                        AdjustmentStatus = null
                    }).ToList();

                return (paged, total);
            }

            var attQuery = ApplyManagerScope(_context.Attendances
                .Where(a => a.CheckIn.Date == date))
                .OrderByDescending(a => a.Id)
                .AsQueryable();

            var attList = await attQuery.ToListAsync();

            var rowList = new List<HRLiveAttendanceRowDto>();
            foreach (var a in attList)
            {
                empDict.TryGetValue(a.EmployeeId ?? 0, out var emp);

                if (!string.IsNullOrWhiteSpace(department) && department != "Tất cả")
                {
                    if (emp == null || !string.Equals(emp.Department, department, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var term = searchTerm.Trim();
                    bool matchName = emp?.Name != null && emp.Name.Contains(term, StringComparison.OrdinalIgnoreCase);
                    bool matchCode = emp?.EmployeeCode != null && emp.EmployeeCode.Contains(term, StringComparison.OrdinalIgnoreCase);
                    bool matchSnapshot = a.EmployeeNameSnapshot != null && a.EmployeeNameSnapshot.Contains(term, StringComparison.OrdinalIgnoreCase);
                    if (!matchName && !matchCode && !matchSnapshot) continue;
                }

                string statusCode = a.CheckOut == null ? "CHECKED_IN" : "CHECKED_OUT";
                string statusText = a.CheckOut == null 
                    ? (a.LateMinutes > 0 ? "Đang làm việc (Đi muộn)" : "Đang làm việc") 
                    : (a.LateMinutes > 0 ? "Đã check-out (Đi muộn)" : "Đã check-out");

                if (!string.IsNullOrWhiteSpace(status) && status != "Tất cả")
                {
                    if (status.Equals("CHECKED_IN", StringComparison.OrdinalIgnoreCase) || status.Equals("Đang làm việc", StringComparison.OrdinalIgnoreCase))
                    {
                        if (statusCode != "CHECKED_IN") continue;
                    }
                    else if (status.Equals("CHECKED_OUT", StringComparison.OrdinalIgnoreCase) || status.Equals("Đã check-out", StringComparison.OrdinalIgnoreCase))
                    {
                        if (statusCode != "CHECKED_OUT") continue;
                    }
                    else if (status.Equals("LATE", StringComparison.OrdinalIgnoreCase) || status.Equals("Đi muộn", StringComparison.OrdinalIgnoreCase))
                    {
                        if (a.LateMinutes <= 0 && !(a.StatusNote != null && a.StatusNote.Contains("muộn"))) continue;
                    }
                }

                double workingHours = a.CheckOut.HasValue 
                    ? a.StandardHours 
                    : Math.Round((DateTime.Now - a.CheckIn).TotalHours, 1);

                rowList.Add(new HRLiveAttendanceRowDto
                {
                    AttendanceId = a.Id,
                    EmployeeId = a.EmployeeId,
                    EmployeeCode = emp?.EmployeeCode ?? a.EmployeeCode ?? "",
                    EmployeeName = emp?.Name ?? a.EmployeeNameSnapshot ?? "Nhân viên",
                    DepartmentName = emp?.Department ?? "",
                    AvatarUrl = emp?.AvatarPath,
                    CheckIn = a.CheckIn,
                    CheckOut = a.CheckOut,
                    StatusCode = statusCode,
                    StatusText = statusText,
                    LateMinutes = a.LateMinutes,
                    WorkingHours = workingHours,
                    StatusNote = a.StatusNote,
                    AdjustmentStatus = a.AdjustmentStatus
                });
            }

            var totalItems = rowList.Count;
            var pagedRows = rowList.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return (pagedRows, totalItems);
        }

        [HttpGet]
        [Authorize(Roles = $"{AppRoles.HR},{AppRoles.Leader},{AppRoles.Manager}")]
        public async Task<IActionResult> GetLiveSummary(DateTime? date)
        {
            if (!CheckLogin()) return Unauthorized();
            var summary = await CalculateLiveSummaryAsync(date ?? DateTime.Today);
            return Json(new { success = true, data = summary });
        }

        [HttpGet]
        [Authorize(Roles = $"{AppRoles.HR},{AppRoles.Leader},{AppRoles.Manager}")]
        public async Task<IActionResult> GetLiveAttendanceSnapshot(DateTime? date, string? department, string? status, string? searchTerm, int page = 1, int pageSize = 50)
        {
            if (!CheckLogin()) return Unauthorized();
            DateTime targetDate = date ?? DateTime.Today;
            var summary = await CalculateLiveSummaryAsync(targetDate);
            var (rows, totalItems) = await QueryLiveAttendanceRowsAsync(targetDate, department, status, searchTerm, page, pageSize);

            return Json(new
            {
                success = true,
                summary = summary,
                rows = rows,
                totalItems = totalItems,
                pageNumber = page,
                pageSize = pageSize,
                serverTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
            });
        }

        // --- 1.1 CONFIG: Thiết lập Quy tắc chấm công ---
        [HttpGet]
        [Authorize(Policy = AppPolicies.HROnly)]
        public IActionResult Config()
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            
            ViewBag.Role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            ViewBag.RealName = HttpContext.Session.GetString("RealName");
            return View();
        }

        // --- 2. CHECK-IN: Lưu trực tiếp vào Database ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIn()
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");

            var currentEmployee = await GetCurrentEmployee();
            if (currentEmployee == null) return Forbid();
            int employeeId = currentEmployee.Id;
            var realName = currentEmployee.Name ?? currentEmployee.EmployeeCode ?? "Employee";

            var strategy = _context.Database.CreateExecutionStrategy();
            bool checkInSuccess = false;
            string? warningMessage = null;
            Attendance? createdAttendance = null;

            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);

                // Authoritative DB check: tìm phiên đang mở chưa Check-out
                var activeRecord = await _context.Attendances
                    .Where(x => x.EmployeeId == employeeId && x.CheckOut == null)
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefaultAsync();

                if (activeRecord != null)
                {
                    warningMessage = "Bạn đang có một phiên làm việc chưa hoàn tất (chưa Check-out). Không thể Check-in trùng lặp.";
                    return;
                }

                // TẠO ĐỐI TƯỢNG MỚI VÀ LƯU VÀO DB
                var attendance = new Attendance
                {
                    EmployeeId = employeeId,
                    EmployeeNameSnapshot = realName, // Keep snapshot for backward compatibility
                    CheckIn = DateTime.Now
                };

                // Tính toán đi muộn / trạng thái bằng Canonical Service (không còn hardcoded 07:30 / 13:00)
                await _attendanceCalc.ProcessCheckInTransactionAsync(attendance);

                _context.Attendances.Add(attendance);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                createdAttendance = attendance;
                checkInSuccess = true;
            });

            if (!checkInSuccess)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json"))
                {
                    return Json(new { success = false, message = warningMessage ?? "Không thể thực hiện Check-in." });
                }
                TempData["Warning"] = warningMessage ?? "Không thể thực hiện Check-in.";
                return RedirectToAction("Index");
            }

            // GỬI THÔNG BÁO REAL-TIME: Chỉ phát sự kiện SAU KHI commit DB thành công
            try
            {
                var empInfo = await _context.Employees.FirstOrDefaultAsync(e => e.Id == employeeId);
                var eventDto = new
                {
                    attendanceId = createdAttendance?.Id ?? 0,
                    employeeId = employeeId,
                    employeeCode = empInfo?.EmployeeCode ?? "",
                    employeeName = empInfo?.Name ?? realName,
                    departmentName = empInfo?.Department ?? "",
                    avatarUrl = empInfo?.AvatarPath,
                    attendanceDate = createdAttendance?.CheckIn.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd"),
                    checkInTime = createdAttendance?.CheckIn.ToString("HH:mm:ss") ?? DateTime.Now.ToString("HH:mm:ss"),
                    checkOutTime = (string?)null,
                    checkInRaw = createdAttendance?.CheckIn.ToString("yyyy-MM-ddTHH:mm:ss"),
                    checkOutRaw = (string?)null,
                    attendanceStatus = "CHECKED_IN",
                    statusText = (createdAttendance?.LateMinutes ?? 0) > 0 ? "Đang làm việc (Đi muộn)" : "Đang làm việc",
                    lateMinutes = createdAttendance?.LateMinutes ?? 0,
                    workingMinutes = 0,
                    workingHours = 0.0,
                    statusNote = createdAttendance?.StatusNote ?? "",
                    updatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                };

                await _hubContext.Clients.Group("role:HR").SendAsync("AttendanceStatusChanged", eventDto);
                int todayCount = await _context.Attendances.CountAsync(x => x.CheckIn.Date == DateTime.Today);
                await _hubContext.Clients.Group("role:HR").SendAsync("UpdateAttendanceCount", todayCount, realName);
            }
            catch (Exception)
            {
                // DB commit succeeded. Never rollback DB if notification delivery fails.
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return Json(new { success = true, message = "Check-in thành công!" });
            }

            TempData["Success"] = "Check-in thành công!";
            return RedirectToAction("Index");
        }

        // --- 3. CHECK-OUT: Cập nhật thông tin vào Database ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOut(int id)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");

            var currentEmployee = await GetCurrentEmployee();
            if (currentEmployee == null) return Forbid();

            var strategy = _context.Database.CreateExecutionStrategy();
            bool checkOutSuccess = false;
            string? errorMessage = null;
            bool isForbidden = false;
            int effectiveId = id;
            Attendance? updatedItem = null;

            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);

                Attendance? item = null;
                if (id > 0)
                {
                    item = await _context.Attendances.FindAsync(id);
                }
                else
                {
                    // Fallback an toàn: tự tìm phiên đang mở của chính nhân viên hiện tại nếu không truyền id
                    item = await _context.Attendances
                        .Where(x => x.EmployeeId == currentEmployee.Id && x.CheckOut == null)
                        .OrderByDescending(x => x.Id)
                        .FirstOrDefaultAsync();
                }

                if (item == null)
                {
                    errorMessage = "Không tìm thấy bản ghi chấm công đang hoạt động để Check-out.";
                    return;
                }

                effectiveId = item.Id;

                // Validate ownership nghiêm ngặt
                if (item.EmployeeId != currentEmployee.Id)
                {
                    isForbidden = true;
                    return;
                }

                // Chặn Checkout trùng lặp / concurrent
                if (item.CheckOut != null)
                {
                    errorMessage = "Bản ghi này đã được check out trước đó!";
                    return;
                }

                item.CheckOut = DateTime.Now;
                
                // LOGIC TÍNH GIỜ (Dùng Canonical Service)
                await _attendanceCalc.ProcessAttendanceTransactionAsync(item);

                _context.Attendances.Update(item);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                updatedItem = item;
                checkOutSuccess = true;
            });

            if (isForbidden)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json"))
                {
                    return StatusCode(403, new { success = false, message = "Bạn không có quyền Check-out bản ghi này." });
                }
                return Forbid();
            }

            if (!checkOutSuccess)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json"))
                {
                    return Json(new { success = false, message = errorMessage ?? "Không thể thực hiện Check-out." });
                }
                TempData["Error"] = errorMessage ?? "Không thể thực hiện Check-out.";
                return RedirectToAction("Index");
            }

            await _auditLogService.LogAsync("CheckOut", "Attendance", "AttendanceCore", effectiveId.ToString(), "CheckOutSuccess", "Success");

            // Chỉ phát SignalR SAU KHI commit DB thành công
            try
            {
                var empInfo = await _context.Employees.FirstOrDefaultAsync(e => e.Id == (updatedItem != null ? updatedItem.EmployeeId : currentEmployee.Id));
                var eventDto = new
                {
                    attendanceId = effectiveId,
                    employeeId = updatedItem?.EmployeeId ?? currentEmployee.Id,
                    employeeCode = empInfo?.EmployeeCode ?? "",
                    employeeName = empInfo?.Name ?? updatedItem?.EmployeeNameSnapshot ?? "Nhân viên",
                    departmentName = empInfo?.Department ?? "",
                    avatarUrl = empInfo?.AvatarPath,
                    attendanceDate = updatedItem?.CheckIn.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd"),
                    checkInTime = updatedItem?.CheckIn.ToString("HH:mm:ss") ?? "--:--:--",
                    checkOutTime = updatedItem?.CheckOut?.ToString("HH:mm:ss"),
                    checkInRaw = updatedItem?.CheckIn.ToString("yyyy-MM-ddTHH:mm:ss"),
                    checkOutRaw = updatedItem?.CheckOut?.ToString("yyyy-MM-ddTHH:mm:ss"),
                    attendanceStatus = "CHECKED_OUT",
                    statusText = (updatedItem?.LateMinutes ?? 0) > 0 ? "Đã check-out (Đi muộn)" : "Đã check-out",
                    lateMinutes = updatedItem?.LateMinutes ?? 0,
                    workingMinutes = (updatedItem?.CheckOut.HasValue == true && updatedItem?.CheckIn != null) ? (int)(updatedItem.CheckOut.Value - updatedItem.CheckIn).TotalMinutes : 0,
                    workingHours = updatedItem?.StandardHours ?? 0.0,
                    statusNote = updatedItem?.StatusNote ?? "",
                    updatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                };

                await _hubContext.Clients.Group("role:HR").SendAsync("AttendanceStatusChanged", eventDto);
                int soBangLuong = await _context.Attendances.CountAsync(x => x.CheckOut != null && x.CheckIn.Date == DateTime.Today);
                await _hubContext.Clients.Group("role:HR").SendAsync("UpdateSalaryCount", soBangLuong);
            }
            catch (Exception)
            {
                // Do not fail transaction if SignalR delivery fails
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return Json(new { success = true, message = "Check-out thành công!" });
            }

            TempData["Success"] = "Check-out thành công!";
            return RedirectToAction("Index");
        }

        // --- 3.1. NHÂN VIÊN/HR YÊU CẦU ĐIỀU CHỈNH CHẤM CÔNG ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestAdjustment(int id, string checkOutTimeStr, string reason)
        {
            if (!CheckLogin()) return Json(new { success = false, message = "Vui lòng đăng nhập" });

            var item = await _context.Attendances.FindAsync(id);
            if (item == null || item.CheckOut != null)
            {
                return Json(new { success = false, message = "Bản ghi không hợp lệ hoặc đã được Check-out" });
            }

            var userRole = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            var currentEmployee = await GetCurrentEmployee();
            if (userRole != "HR" && userRole != "Leader" && (currentEmployee == null || item.EmployeeId != currentEmployee.Id))
            {
                return Json(new { success = false, message = "Bạn không có quyền yêu cầu điều chỉnh cho người khác" });
            }

            if (!TimeSpan.TryParse(checkOutTimeStr, out TimeSpan time))
            {
                return Json(new { success = false, message = "Định dạng thời gian không hợp lệ" });
            }

            item.AdjustmentStatus = "PENDING";
            item.RequestedCheckOutTime = time;
            item.AdjustmentReason = reason;
            
            _context.Attendances.Update(item);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã gửi yêu cầu điều chỉnh thành công!" });
        }

        // --- 3.2. HR/ADMIN DUYỆT HOẶC ÁP DỤNG ĐIỀU CHỈNH ---
        [HttpPost]
        [Authorize(Roles = $"{AppRoles.Manager},{AppRoles.HR}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewAdjustment(int id, string actionType)
        {
            if (!CheckLogin()) return Json(new { success = false, message = "Vui lòng đăng nhập" });

            var userRole = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            var item = await _context.Attendances.FindAsync(id);
            if (item == null)
            {
                return Json(new { success = false, message = "Bản ghi không tồn tại" });
            }

            if (actionType == "APPROVE" || actionType == "REJECT")
            {
                // Business Rule: Chỉ Manager có active scope mới có quyền duyệt / từ chối
                if (userRole != AppRoles.Manager)
                {
                    return Json(new { success = false, message = "Chỉ Quản lý trực tiếp (Manager) mới có quyền duyệt yêu cầu điều chỉnh công." });
                }

                if (item.AdjustmentStatus != "PENDING" && item.AdjustmentStatus != "Chờ duyệt")
                {
                    return Json(new { success = false, message = "Yêu cầu này đã được xử lý trước đó!" });
                }

                var employee = item.EmployeeId.HasValue ? await _context.Employees.FindAsync(item.EmployeeId.Value) : null;
                if (employee == null || !_managerScope.CanAccessDepartment(employee.Department))
                {
                    return Json(new { success = false, message = "Bạn không có quyền duyệt cho nhân viên ngoài phạm vi quản lý." });
                }

                var currentEmp = await GetCurrentEmployee();
                if (currentEmp != null && item.EmployeeId == currentEmp.Id)
                {
                    return Json(new { success = false, message = "Bạn không thể tự duyệt yêu cầu của chính mình." });
                }

                item.AdjustmentStatus = actionType == "APPROVE" ? "APPROVED" : "REJECTED";
            }
            else if (actionType == "APPLY")
            {
                // HR vận hành áp dụng công đã được duyệt
                if (userRole != AppRoles.HR)
                {
                    return Json(new { success = false, message = "Chỉ HR mới có quyền áp dụng điều chỉnh công vào hệ thống." });
                }
                if (item.AdjustmentStatus != "APPROVED")
                {
                    return Json(new { success = false, message = "Yêu cầu phải được duyệt trước khi áp dụng" });
                }
                
                if (item.RequestedCheckOutTime.HasValue)
                {
                    DateTime cáo = item.CheckIn.Date.Add(item.RequestedCheckOutTime.Value);
                    if (cáo <= item.CheckIn)
                    {
                        return Json(new { success = false, message = "Giờ Check-out đề xuất không hợp lệ (nhỏ sau giờ Check-in)" });
                    }

                    item.CheckOut = cáo;

                    // Tính toán quỹ công bằng Canonical Service
                    await _attendanceCalc.ProcessAttendanceTransactionAsync(item);

                    var realName = HttpContext.Session.GetString("RealName") ?? userRole;
                    string auditNote = $"[Yêu cầu điều chỉnh ({item.RequestedCheckOutTime.Value:hh\\:mm}) được áp dụng bởi {realName} ({DateTime.Now:dd/MM/yyyy HH:mm}): {item.AdjustmentReason}]";
                    item.StatusNote = (item.StatusNote ?? "") + " " + auditNote;
                    item.AdjustmentStatus = "APPLIED";
                }
            }
            else
            {
                return Json(new { success = false, message = "Thao tác không hợp lệ" });
            }

            _context.Attendances.Update(item);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync("ReviewAdjustment", "Attendance", "AttendanceCore", item.Id.ToString(), $"Action:{actionType}", "Success");

            return Json(new { success = true });
        }

        // --- 4. API LẤY LỊCH SỬ CHO FULLCALENDAR ---
        [HttpGet]
        public async Task<IActionResult> GetAttendanceHistory()
        {
            var userRole = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            IQueryable<Attendance> query = ApplyManagerScope(_context.Attendances);
            if (userRole == "Employee")
            {
                var currentEmployee = await GetCurrentEmployee();
                if (currentEmployee == null) return Forbid();
                query = query.Where(a => a.EmployeeId == currentEmployee.Id);
            }
            
            var data = await query.ToListAsync();
            var events = data.GroupBy(a => a.CheckIn.Date).Select(g => new
            {
                title = $"{g.Count()} lượt chấm công",
                start = g.Key.ToString("yyyy-MM-dd"),
                color = "#0284c7",
                textColor = "white",
                allDay = true
            }).ToList();

            return Json(events);
        }

        // --- 5. LOGIC ĐÁNH GIÁ (DÀNH RIÊNG CHO POPUP CHI TIẾT) ---
        private (bool IsLate, string Note) EvaluateAttendance(DateTime checkInTime)
        {
            TimeSpan time = checkInTime.TimeOfDay;
            if (time <= new TimeSpan(7, 0, 0)) return (false, "Đúng giờ (Ca sáng)");
            if (time > new TimeSpan(7, 0, 0) && time <= new TimeSpan(11, 0, 0)) return (true, "Đi muộn (Ca sáng)");
            if (time > new TimeSpan(11, 0, 0) && time <= new TimeSpan(13, 0, 0)) return (false, "Đúng giờ (Ca chiều)");
            if (time > new TimeSpan(13, 0, 0) && time <= new TimeSpan(17, 0, 0)) return (true, "Đi muộn (Ca chiều)");
            if (time > new TimeSpan(17, 0, 0) && time <= new TimeSpan(19, 30, 0)) return (false, "Tăng ca (Tối)");
            return (true, "Ngoài giờ (Bất thường)");
        }

        // --- 6. API LẤY CHI TIẾT THEO NGÀY ---
        [HttpGet]
        public async Task<IActionResult> GetAttendanceDetailsByDate(string date)
        {
            if (!DateTime.TryParse(date, out DateTime selectedDate)) return BadRequest();

            var userRole = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            IQueryable<Attendance> query = ApplyManagerScope(_context.Attendances.Where(a => a.CheckIn.Date == selectedDate.Date));
            if (userRole == "Employee")
            {
                var currentEmployee = await GetCurrentEmployee();
                if (currentEmployee == null) return Forbid();
                query = query.Where(a => a.EmployeeId == currentEmployee.Id);
            }

            var data = await query.ToListAsync();

            var details = data.Select(a => {
                var eval = EvaluateAttendance(a.CheckIn);
                return new
                {
                    empName = a.EmployeeName,
                    checkIn = a.CheckIn.ToString("HH:mm:ss"),
                    checkOut = a.CheckOut.HasValue ? a.CheckOut.Value.ToString("HH:mm:ss") : "---",
                    isLate = eval.IsLate,
                    note = eval.Note
                };
            }).ToList();

            return Json(details);
        }

        private double CalculateOverlap(DateTime start1, DateTime end1, DateTime start2, DateTime end2)
        {
            DateTime overlapStart = start1 > start2 ? start1 : start2;
            DateTime overlapEnd = end1 < end2 ? end1 : end2;
            return (overlapStart < overlapEnd) ? (overlapEnd - overlapStart).TotalHours : 0;
        }

        // =========================================================
        // MY ATTENDANCE / EMPLOYEE SELF-SERVICE ENDPOINTS
        // =========================================================

        private async Task<Employee?> GetCurrentEmployeeAsync()
        {
            return await GetCurrentEmployee();
        }

        private async Task<WorkShift?> GetEffectiveShiftAsync(Employee emp)
        {
            return await _attendanceCalc.GetEffectiveWorkShiftAsync(emp.EmployeeCode ?? "", emp.Department ?? "", DateTime.Today);
        }

        [HttpGet]
        public async Task<IActionResult> GetMyAttendanceToday()
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Unauthorized();

            var today = DateTime.Today;

            // Ưu tiên tìm phiên đang mở (CheckOut == null) trước, sau đó tìm bản ghi hôm nay
            var activeAtt = await _context.Attendances
                .Where(a => a.EmployeeId == emp.Id && a.CheckOut == null)
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync();

            var att = activeAtt ?? await _context.Attendances
                .Where(a => a.EmployeeId == emp.Id && a.CheckIn.Date == today)
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync();

            var shift = await GetEffectiveShiftAsync(emp);

            string statusCode;
            string statusText;
            if (att == null)
            {
                statusCode = "NOT_CHECKED_IN";
                statusText = "Chưa chấm công";
            }
            else if (att.CheckOut == null)
            {
                statusCode = "CHECKED_IN";
                statusText = "Đang làm việc";
            }
            else
            {
                statusCode = "CHECKED_OUT";
                statusText = "Đã check-out";
            }

            double workedHours = 0;
            if (att != null)
            {
                if (att.CheckOut.HasValue)
                {
                    workedHours = (att.CheckOut.Value - att.CheckIn).TotalHours;
                }
                else
                {
                    workedHours = Math.Max(0, (DateTime.Now - att.CheckIn).TotalHours);
                }
            }

            return Json(new {
                success = true,
                data = new {
                    attendanceId = att?.Id,
                    date = today.ToString("yyyy-MM-dd"),
                    serverTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                    shiftName = shift?.ShiftName ?? "Ca Hành chính",
                    shiftStart = shift?.StartTime.ToString(@"hh\:mm") ?? "08:00",
                    shiftEnd = shift?.EndTime.ToString(@"hh\:mm") ?? "17:00",
                    checkIn = att?.CheckIn.ToString("HH:mm:ss") ?? "",
                    checkOut = att?.CheckOut?.ToString("HH:mm:ss") ?? "",
                    checkInRaw = att?.CheckIn.ToString("yyyy-MM-ddTHH:mm:ss"),
                    checkOutRaw = att?.CheckOut?.ToString("yyyy-MM-ddTHH:mm:ss"),
                    statusCode = statusCode,
                    status = statusText,
                    canCheckIn = (att == null || att.CheckOut != null),
                    canCheckOut = (att != null && att.CheckOut == null),
                    workedHours = workedHours,
                    lateMinutes = att?.LateMinutes ?? 0,
                    statusNote = att?.StatusNote ?? ""
                }
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetMyMonthlySummary(int year, int month)
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Unauthorized();

            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var list = await _context.Attendances
                .Where(a => a.EmployeeId == emp.Id && a.CheckIn >= startDate && a.CheckIn <= endDate.AddDays(1))
                .ToListAsync();

            // Calculate business days as standard days
            int standardWorkDays = 0;
            for (var d = startDate; d <= endDate; d = d.AddDays(1))
            {
                if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) standardWorkDays++;
            }

            var actualWorkDays = list.Select(a => a.CheckIn.Date).Distinct().Count();
            var lateCount = list.Count(a => a.LateMinutes > 0 || (a.StatusNote != null && a.StatusNote.Contains("muộn")));
            var earlyLeaveCount = list.Count(a => a.StatusNote != null && a.StatusNote.Contains("sớm"));
            var overtimeHours = list.Sum(a => a.OvertimeHours);

            return Json(new {
                success = true,
                data = new {
                    standardWorkDays = standardWorkDays,
                    actualWorkDays = actualWorkDays,
                    lateCount = lateCount,
                    earlyLeaveCount = earlyLeaveCount,
                    leaveDays = 0, // Should integrate with leave module
                    overtimeHours = overtimeHours
                }
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetMyMonthlyCalendar(int year, int month)
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Unauthorized();

            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var list = await _context.Attendances
                .Where(a => a.EmployeeId == emp.Id && a.CheckIn >= startDate && a.CheckIn <= endDate.AddDays(1))
                .ToListAsync();

            var calendarData = list.GroupBy(a => a.CheckIn.Date).Select(g => {
                var first = g.First();
                var isLate = first.LateMinutes > 0 || (first.StatusNote != null && first.StatusNote.Contains("muộn"));
                var isMissingCheckout = first.CheckOut == null && first.CheckIn.Date < DateTime.Today;
                var hasExplanation = first.AdjustmentStatus != null;
                
                string primaryStatus = "Bình thường";
                if (isMissingCheckout) primaryStatus = "Thiếu Check-out";
                else if (isLate) primaryStatus = "Đi muộn";
                else if (first.OvertimeHours > 0) primaryStatus = "Có Tăng ca";

                return new {
                    date = g.Key.ToString("yyyy-MM-dd"),
                    primaryStatus = primaryStatus,
                    hasExplanation = hasExplanation,
                    isLate = isLate,
                    checkIn = first.CheckIn.ToString("HH:mm")
                };
            }).ToList();

            return Json(new { success = true, data = calendarData });
        }

        [HttpGet]
        public async Task<IActionResult> GetMyAttendanceDetail(string date)
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Unauthorized();

            if (!DateTime.TryParse(date, out DateTime parsedDate)) return BadRequest();

            var list = await _context.Attendances
                .Where(a => a.EmployeeId == emp.Id && a.CheckIn.Date == parsedDate.Date)
                .OrderBy(a => a.CheckIn)
                .ToListAsync();

            var reqs = await _context.AttendanceAdjustmentRequests
                .Where(r => r.EmployeeCode == emp.EmployeeCode && list.Select(a => a.Id).Contains(r.AttendanceId))
                .ToListAsync();

            var shift = await GetEffectiveShiftAsync(emp);

            var result = list.Select(a => new {
                id = a.Id,
                checkIn = a.CheckIn.ToString("HH:mm:ss"),
                checkOut = a.CheckOut?.ToString("HH:mm:ss") ?? "",
                shiftName = shift?.ShiftName ?? "Ca Hành chính",
                standardHours = a.StandardHours,
                overtimeHours = a.OvertimeHours,
                lateMinutes = a.LateMinutes,
                statusNote = a.StatusNote,
                adjustmentStatus = a.AdjustmentStatus
            });

            return Json(new { success = true, data = result, requests = reqs.Select(r => new { r.Id, r.Status, r.Reason, r.RequestedCheckOut }) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitExplanation(string dateStr, TimeSpan? proposedCheckOut, string reason)
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Unauthorized();

            if (!DateTime.TryParse(dateStr, out DateTime parsedDate)) 
                return Json(new { success = false, message = "Ngày không hợp lệ" });

            var att = await _context.Attendances
                .Where(a => a.EmployeeId == emp.Id && a.CheckIn.Date == parsedDate.Date)
                .FirstOrDefaultAsync();

            if (att == null) return Json(new { success = false, message = "Không tìm thấy dữ liệu chấm công ngày này." });

            var req = new AttendanceAdjustmentRequest
            {
                AttendanceId = att.Id,
                EmployeeCode = emp.EmployeeCode ?? emp.Name ?? "",
                Reason = reason,
                RequestedCheckOut = proposedCheckOut,
                Status = "PENDING",
                CreatedAt = DateTime.Now
            };

            _context.AttendanceAdjustmentRequests.Add(req);
            att.AdjustmentStatus = "PENDING";
            
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã gửi yêu cầu giải trình thành công" });
        }

        [HttpGet]
        public async Task<IActionResult> GetMyRecentExplanations()
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Unauthorized();

            var list = await _context.AttendanceAdjustmentRequests
                .Where(r => r.EmployeeCode == emp.EmployeeCode || r.EmployeeCode == emp.Name)
                .OrderByDescending(r => r.CreatedAt)
                .Take(10)
                .Select(r => new {
                    id = r.Id,
                    attendanceDate = _context.Attendances.Where(a => a.Id == r.AttendanceId).Select(a => a.CheckIn).FirstOrDefault().ToString("yyyy-MM-dd"),
                    reason = r.Reason,
                    status = r.Status,
                    createdAt = r.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                })
                .ToListAsync();

            return Json(new { success = true, data = list });
        }
    }
}
