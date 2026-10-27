using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace giao_dien_demo.Controllers
{
    [Authorize(Roles = $"{AppRoles.HR},{AppRoles.Leader}")]
    [AutoValidateAntiforgeryToken]
    public class OnboardingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.IAuditLogService _auditLogService;
        private readonly giao_dien_demo.Services.ILeadershipScopeService _leadershipScope;

        public OnboardingController(
            ApplicationDbContext context,
            giao_dien_demo.Services.IAuditLogService auditLogService,
            giao_dien_demo.Services.ILeadershipScopeService leadershipScope)
        {
            _context = context;
            _auditLogService = auditLogService;
            _leadershipScope = leadershipScope;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 1. INDEX — Danh sách hồ sơ hội nhập
        // ─────────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index(
            string? searchName,
            string? department,
            string? status,
            int? month,
            int? year)
        {
            var role = CurrentActor.Role(User);
            var isLeader = role == AppRoles.Leader;
            var leaderUnits = isLeader ? _leadershipScope.GetOverseenOrganizationalUnits() : new List<string>();

            // --- TRUY VẤN CƠ BẢN ---
            var query = _context.OnboardingRecords
                .Include(o => o.Employee)
                .Include(o => o.ChecklistTasks)
                .AsQueryable();

            // --- DATA SCOPE CHO LEADER ---
            if (isLeader)
            {
                if (leaderUnits.Count == 0)
                {
                    query = query.Where(o => false);
                }
                else
                {
                    query = query.Where(o =>
                        (o.Department != null && leaderUnits.Contains(o.Department)) ||
                        (o.Department == null && o.Employee != null && o.Employee.Department != null && leaderUnits.Contains(o.Employee.Department)));
                }
            }

            // --- LỌC THEO TÊN / MÃ NV ---
            if (!string.IsNullOrWhiteSpace(searchName))
            {
                query = query.Where(o =>
                    (o.Employee != null && o.Employee.Name != null && o.Employee.Name.Contains(searchName)) ||
                    (o.Employee != null && o.Employee.EmployeeCode != null && o.Employee.EmployeeCode.Contains(searchName)));
            }

            // --- LỌC THEO PHÒNG BAN ---
            if (!string.IsNullOrWhiteSpace(department))
            {
                query = query.Where(o => o.Employee != null && o.Employee.Department == department);
            }

            // --- LỌC THEO TRẠNG THÁI ---
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(o => o.OnboardingStatus == status);
            }

            // --- LỌC THEO THÁNG ---
            if (month.HasValue && month > 0)
            {
                query = query.Where(o => o.JoinDate.HasValue && o.JoinDate.Value.Month == month);
            }

            // --- LỌC THEO NĂM ---
            if (year.HasValue && year > 0)
            {
                query = query.Where(o => o.JoinDate.HasValue && o.JoinDate.Value.Year == year);
            }

            var records = await query
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            // --- STATISTICS (tính từ database theo scope) ---
            var allRecordsQuery = _context.OnboardingRecords
                .Include(o => o.Employee)
                .AsQueryable();

            if (isLeader)
            {
                if (leaderUnits.Count == 0)
                {
                    allRecordsQuery = allRecordsQuery.Where(o => false);
                }
                else
                {
                    allRecordsQuery = allRecordsQuery.Where(o =>
                        (o.Department != null && leaderUnits.Contains(o.Department)) ||
                        (o.Department == null && o.Employee != null && o.Employee.Department != null && leaderUnits.Contains(o.Employee.Department)));
                }
            }

            var allRecords = await allRecordsQuery.ToListAsync();

            if (isLeader)
            {
                ViewBag.StatNewEmployees = await _leadershipScope.GetScopedEmployeeQuery().CountAsync();
                ViewBag.Departments = leaderUnits;
            }
            else
            {
                ViewBag.StatNewEmployees = await _context.Employees.CountAsync(e => e.Id > 0);
                ViewBag.Departments = await _context.Employees
                    .Where(e => e.Department != null)
                    .Select(e => e.Department!)
                    .Distinct()
                    .ToListAsync();
            }

            ViewBag.StatPending = allRecords.Count(r => r.Status == "PENDING");
            ViewBag.StatInProgress = allRecords.Count(r => r.Status == "IN_PROGRESS");
            ViewBag.StatCompleted = allRecords.Count(r => r.Status == "COMPLETED");

            ViewBag.CurrentSearch = searchName;
            ViewBag.CurrentDept = department;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentMonth = month;
            ViewBag.CurrentYear = year;

            return View(records);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 2. DETAIL — Chi tiết hồ sơ (AJAX/JSON cho drawer)
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var record = await _context.OnboardingRecords
                .Include(o => o.Employee)
                .Include(o => o.ChecklistTasks)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (record == null)
                return NotFound(new { message = "Không tìm thấy hồ sơ hội nhập." });

            var role = CurrentActor.Role(User);
            if (role == AppRoles.Leader)
            {
                var targetDept = record.Department ?? record.Employee?.Department;
                if (!_leadershipScope.CanAccessDepartment(targetDept))
                {
                    return Forbid();
                }
            }

            // Tính tiến độ real-time từ checklist
            int totalTasks = record.ChecklistTasks.Count;
            int doneTasks = record.ChecklistTasks.Count(t => t.Status == "DONE");
            int mandatoryTotal = record.ChecklistTasks.Count(t => t.IsMandatory);
            int mandatoryDone = record.ChecklistTasks.Count(t => t.IsMandatory && t.Status == "DONE");

            // Lấy hợp đồng nhân viên (nếu có)
            var contracts = await _context.Contracts
                .Where(c => c.EmployeeId == record.EmployeeId)
                .OrderByDescending(c => c.CreatedAt)
                .Take(3)
                .ToListAsync();

            return Json(new
            {
                id = record.Id,
                employeeId = record.EmployeeId,
                employeeCode = record.Employee?.EmployeeCode ?? "---",
                employeeName = record.Employee?.Name ?? "---",
                department = record.Employee?.Department ?? "---",
                jobTitle = record.Employee?.JobTitle ?? "---",
                position = record.Employee?.Position ?? "---",
                avatarPath = record.Employee?.AvatarPath,
                startDate = record.StartDate.ToString("dd/MM/yyyy"),
                expectedEndDate = record.ExpectedEndDate?.ToString("dd/MM/yyyy"),
                actualEndDate = record.ActualEndDate?.ToString("dd/MM/yyyy"),
                status = record.Status,
                statusLabel = record.StatusLabel,
                progressPercent = totalTasks > 0 ? (int)Math.Round((double)doneTasks / totalTasks * 100) : 0,
                assignedToName = record.AssignedToName ?? "---",
                notes = record.Notes ?? "",
                createdAt = record.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                totalTasks,
                doneTasks,
                mandatoryTotal,
                mandatoryDone,
                checklistTasks = record.ChecklistTasks
                    .OrderBy(t => t.SortOrder)
                    .Select(t => new
                    {
                        t.Id,
                        t.TaskName,
                        t.Category,
                        t.IsMandatory,
                        t.Status,
                        statusLabel = t.StatusLabel,
                        t.AssigneeName,
                        deadline = t.Deadline?.ToString("dd/MM/yyyy"),
                        completedAt = t.CompletedAt?.ToString("dd/MM/yyyy"),
                        t.Notes
                    }),
                contracts = contracts.Select(c => new
                {
                    c.Id,
                    duration = c.Duration ?? "---",
                    startDate = c.StartDate.ToString("dd/MM/yyyy"),
                    endDate = c.EndDate.ToString("dd/MM/yyyy"),
                    c.Status
                })
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // 3. GET EMPLOYEES — Cho Employee selector trong Create form
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetEmployees(string? q)
        {
            var role = CurrentActor.Role(User);
            var query = role == AppRoles.Leader
                ? _leadershipScope.GetScopedEmployeeQuery()
                : _context.Employees.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(e =>
                    (e.Name != null && e.Name.Contains(q)) ||
                    (e.EmployeeCode != null && e.EmployeeCode.Contains(q)));
            }

            var employees = await query
                .OrderBy(e => e.EmployeeCode)
                .Take(20)
                .Select(e => new
                {
                    e.Id,
                    e.EmployeeCode,
                    e.Name,
                    e.Department,
                    e.JobTitle,
                    e.Position,
                    e.AvatarPath
                })
                .ToListAsync();

            return Json(employees);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 4. CREATE — Tạo hồ sơ hội nhập mới
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> Create([FromBody] OnboardingRecord model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var employee = await _context.Employees
                    .FirstOrDefaultAsync(x => x.Id == model.EmployeeId);

                if (employee == null)
                {
                    return BadRequest(new
                    {
                        message = "EmployeeId không tồn tại trong bảng Employees."
                    });
                }

                model.EmployeeCode = employee.EmployeeCode;
                model.EmployeeName = employee.Name;
                model.Department = employee.Department;
                model.JobTitle = employee.JobTitle;

                // Set default values for NOT NULL columns in DB
                model.DocumentStatus = string.IsNullOrEmpty(model.DocumentStatus) ? "Chưa nộp" : model.DocumentStatus;
                // Lifecycle is server-owned; a create request can never start or complete a record.
                model.OnboardingStatus = "PENDING";
                model.ProgressPercent = 0;
                model.ActualEndDate = null;

                model.CreatedAt = DateTime.Now;
                model.UpdatedAt = DateTime.Now;

                _context.OnboardingRecords.Add(model);
                await _context.SaveChangesAsync();

                _ = _auditLogService.LogAsync("Create", "EmployeeLifecycle", "Onboarding", model.Id.ToString(), "Activity", "Success");

                return Ok(new
                {
                    success = true,
                    message = "Tạo hồ sơ hội nhập thành công.",
                    id = model.Id
                });
            }
            catch (Exception ex)
            {
                string errMsg = ex.Message;
                if (ex.InnerException != null) 
                {
                    errMsg += " - Inner: " + ex.InnerException.Message;
                }
                return StatusCode(500, new
                {
                    success = false,
                    message = errMsg
                });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 5. UPDATE TASK STATUS — Cập nhật trạng thái checklist item
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> UpdateTaskStatus(int taskId, string newStatus)
        {
            var task = await _context.OnboardingChecklistTasks
                .Include(t => t.OnboardingRecord)
                    .ThenInclude(r => r!.ChecklistTasks)
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null)
                return NotFound(new { message = "Không tìm thấy công việc." });

            if (task.OnboardingRecord?.Status == "COMPLETED" || task.OnboardingRecord?.Status == "CANCELLED")
                return BadRequest(new { message = "Không thể cập nhật checklist của hồ sơ đã kết thúc." });

            var validStatuses = new[] { "PENDING", "IN_PROGRESS", "DONE", "SKIPPED" };
            if (!validStatuses.Contains(newStatus))
                return BadRequest(new { message = "Trạng thái không hợp lệ." });

            task.Status = newStatus;
            if (newStatus == "DONE") task.CompletedAt = DateTime.Now;
            else task.CompletedAt = null;

            // --- Tự động cập nhật tiến độ hồ sơ ---
            if (task.OnboardingRecord != null)
            {
                var allTasks = task.OnboardingRecord.ChecklistTasks.ToList();
                int total = allTasks.Count;
                int done = allTasks.Count(t => t.Status == "DONE" || (t.Id == taskId && newStatus == "DONE"))
                         - allTasks.Count(t => t.Id == taskId && t.Status == "DONE")
                         + (newStatus == "DONE" ? 1 : 0);

                // Tính lại đúng
                done = allTasks.Count(t => t.Id != taskId && t.Status == "DONE") + (newStatus == "DONE" ? 1 : 0);

                task.OnboardingRecord.ProgressPercent = total > 0 ? (int)Math.Round((double)done / total * 100) : 0;
                task.OnboardingRecord.UpdatedAt = DateTime.Now;

                // Kiểm tra: nếu tất cả required tasks DONE → có thể complete
                bool allMandatoryDone = allTasks
                    .Where(t => t.IsMandatory && t.Id != taskId)
                    .All(t => t.Status == "DONE")
                    && (task.IsMandatory ? newStatus == "DONE" : true);

                if (allMandatoryDone && task.OnboardingRecord.Status == "PENDING")
                    task.OnboardingRecord.Status = "IN_PROGRESS";
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Cập nhật thành công.",
                newProgress = task.OnboardingRecord?.ProgressPercent ?? 0
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // 6. COMPLETE ONBOARDING — Đánh dấu hoàn thành
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> Complete(int id)
        {
            var record = await _context.OnboardingRecords
                .Include(o => o.ChecklistTasks)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (record == null)
                return NotFound(new { message = "Không tìm thấy hồ sơ hội nhập." });

            if (record.Status == "COMPLETED")
                return BadRequest(new { message = "Hồ sơ đã ở trạng thái Hoàn thành." });
            if (record.Status == "CANCELLED")
                return BadRequest(new { message = "Không thể hoàn thành hồ sơ đã hủy." });

            // Business rule: Còn required task chưa hoàn thành → không được complete
            var incompleteRequired = record.ChecklistTasks
                .Where(t => t.IsMandatory && t.Status != "DONE")
                .ToList();

            if (incompleteRequired.Any())
            {
                return BadRequest(new
                {
                    message = $"Còn {incompleteRequired.Count} công việc bắt buộc chưa hoàn thành.",
                    incompleteCount = incompleteRequired.Count
                });
            }

            record.Status = "COMPLETED";
            record.ActualEndDate = DateTime.Today;
            record.ProgressPercent = 100;
            record.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            _ = _auditLogService.LogAsync("Complete", "EmployeeLifecycle", "Onboarding", id.ToString(), "Activity", "Success");

            return Ok(new { message = "Đã đánh dấu hội nhập hoàn thành." });
        }

        // ─────────────────────────────────────────────────────────────────────
        // 7. START ONBOARDING — Chuyển PENDING → IN_PROGRESS
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> Start(int id)
        {
            var record = await _context.OnboardingRecords.FindAsync(id);
            if (record == null)
                return NotFound(new { message = "Không tìm thấy hồ sơ." });

            if (record.Status != "PENDING")
                return BadRequest(new { message = "Chỉ có thể bắt đầu hồ sơ ở trạng thái Chờ bắt đầu." });

            record.Status = "IN_PROGRESS";
            record.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            _ = _auditLogService.LogAsync("Start", "EmployeeLifecycle", "Onboarding", id.ToString(), "Activity", "Success");

            return Ok(new { message = "Đã bắt đầu hội nhập." });
        }

        // ─────────────────────────────────────────────────────────────────────
        // 8. CANCEL — Hủy hồ sơ
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> Cancel(int id, string? reason)
        {
            var record = await _context.OnboardingRecords.FindAsync(id);
            if (record == null)
                return NotFound(new { message = "Không tìm thấy hồ sơ." });

            if (record.Status == "COMPLETED" || record.Status == "CANCELLED")
                return BadRequest(new { message = "Hồ sơ đã ở trạng thái kết thúc." });

            record.Status = "CANCELLED";
            record.Notes = string.IsNullOrEmpty(reason)
                ? record.Notes
                : (record.Notes + "\n[HỦY]: " + reason);
            record.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            _ = _auditLogService.LogAsync("Cancel", "EmployeeLifecycle", "Onboarding", id.ToString(), "Activity", "Success", null, null, null, reason);

            return Ok(new { message = "Đã hủy hồ sơ hội nhập." });
        }

        // ─────────────────────────────────────────────────────────────────────
        // 9. EXPORT EXCEL — Xuất danh sách (CSV đơn giản, không cần thư viện)
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> ExportExcel(
            string? searchName,
            string? department,
            string? status,
            int? month,
            int? year)
        {
            var query = _context.OnboardingRecords
                .Include(o => o.Employee)
                .Include(o => o.ChecklistTasks)
                .AsQueryable();

            var role = CurrentActor.Role(User);
            if (role == AppRoles.Leader)
            {
                var leaderUnits = _leadershipScope.GetOverseenOrganizationalUnits();
                if (leaderUnits.Count == 0)
                {
                    query = query.Where(o => false);
                }
                else
                {
                    query = query.Where(o =>
                        (o.Department != null && leaderUnits.Contains(o.Department)) ||
                        (o.Department == null && o.Employee != null && o.Employee.Department != null && leaderUnits.Contains(o.Employee.Department)));
                }
            }

            if (!string.IsNullOrWhiteSpace(searchName))
                query = query.Where(o =>
                    (o.Employee != null && o.Employee.Name != null && o.Employee.Name.Contains(searchName)) ||
                    (o.Employee != null && o.Employee.EmployeeCode != null && o.Employee.EmployeeCode.Contains(searchName)));

            if (!string.IsNullOrWhiteSpace(department))
                query = query.Where(o => o.Employee != null && o.Employee.Department == department);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(o => o.OnboardingStatus == status);

            if (month.HasValue && month > 0)
                query = query.Where(o => o.JoinDate.HasValue && o.JoinDate.Value.Month == month);

            if (year.HasValue && year > 0)
                query = query.Where(o => o.JoinDate.HasValue && o.JoinDate.Value.Year == year);

            var records = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

            // Tạo CSV
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("STT,Mã NV,Họ tên,Phòng ban,Chức danh,Ngày bắt đầu,Dự kiến hoàn thành,Người phụ trách,Tiến độ (%),Trạng thái");

            int idx = 1;
            foreach (var r in records)
            {
                int totalT = r.ChecklistTasks.Count;
                int doneT = r.ChecklistTasks.Count(t => t.Status == "DONE");
                int prog = totalT > 0 ? (int)Math.Round((double)doneT / totalT * 100) : 0;

                sb.AppendLine(string.Join(",",
                    idx++,
                    r.Employee?.EmployeeCode ?? "",
                    $"\"{r.Employee?.Name ?? ""}\"",
                    $"\"{r.Employee?.Department ?? ""}\"",
                    $"\"{r.Employee?.JobTitle ?? ""}\"",
                    r.StartDate.ToString("dd/MM/yyyy"),
                    r.ExpectedEndDate?.ToString("dd/MM/yyyy") ?? "",
                    $"\"{r.AssignedToName ?? ""}\"",
                    prog,
                    r.StatusLabel
                ));
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))
                .ToArray();

            string fileName = $"HoiNhap_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }
    }
}
