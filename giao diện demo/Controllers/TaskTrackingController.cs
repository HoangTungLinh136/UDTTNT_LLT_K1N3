using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.HROnly)]
    public class TaskTrackingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TaskTrackingController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Prepare some basic data for the view
            ViewBag.Employees = await _context.Employees
                .Select(e => new { e.Id, e.Name, e.EmployeeCode, e.Department })
                .ToListAsync();

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetStatistics()
        {
            var tasks = await _context.HrTasks.ToListAsync();
            var now = DateTime.Now;

            int openCount = tasks.Count(t => t.Status == "OPEN" || t.Status == "IN_PROGRESS");
            int completedCount = tasks.Count(t => t.Status == "COMPLETED");
            
            // Sắp hết hạn (Near due): chưa hoàn thành, chưa hủy và Deadline trong vòng 24h tới
            int nearDueCount = tasks.Count(t => 
                (t.Status == "OPEN" || t.Status == "IN_PROGRESS") && 
                t.Deadline > now && 
                (t.Deadline - now).TotalHours <= 24);

            // Quá hạn (Overdue): chưa hoàn thành, chưa hủy và Deadline < now
            int overdueCount = tasks.Count(t => 
                (t.Status == "OPEN" || t.Status == "IN_PROGRESS") && 
                t.Deadline < now);

            return Json(new {
                open = openCount,
                nearDue = nearDueCount,
                overdue = overdueCount,
                completed = completedCount
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetTasks(string? assignee, string? priority, string? status)
        {
            var query = _context.HrTasks.AsQueryable();

            if (!string.IsNullOrEmpty(assignee))
            {
                if (int.TryParse(assignee, out int assigneeId))
                {
                    query = query.Where(t => t.AssigneeEmployeeId == assigneeId);
                }
            }
            if (!string.IsNullOrEmpty(priority))
            {
                query = query.Where(t => t.Priority == priority);
            }
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(t => t.Status == status);
            }

            var tasks = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
            var now = DateTime.Now;

            var result = tasks.Select(t => {
                string deadlineState = "NORMAL";
                if (t.Status != "COMPLETED" && t.Status != "CANCELLED")
                {
                    if (t.Deadline < now) deadlineState = "OVERDUE";
                    else if ((t.Deadline - now).TotalHours <= 24) deadlineState = "NEAR_DUE";
                }

                return new {
                    id = t.Id,
                    taskCode = t.TaskCode,
                    title = t.Title,
                    relatedModule = t.RelatedModule,
                    relatedRecordCode = t.RelatedRecordCode,
                    assignee = t.AssigneeNameSnapshot,
                    deadline = t.Deadline.ToString("dd/MM/yyyy HH:mm"),
                    priority = t.Priority,
                    status = t.Status,
                    progress = t.ProgressPercent,
                    deadlineState = deadlineState,
                    updatedAt = t.UpdatedAt.ToString("dd/MM/yyyy HH:mm")
                };
            }).ToList();

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetTaskDetail(int id)
        {
            var task = await _context.HrTasks.FindAsync(id);
            if (task == null) return NotFound();

            if (task.Status == "COMPLETED" || task.Status == "CANCELLED")
                return Json(new { success = false, message = "Không thể cập nhật công việc đã kết thúc." });

            var history = await _context.HrTaskHistories
                .Where(h => h.HrTaskId == id)
                .OrderByDescending(h => h.Timestamp)
                .Select(h => new {
                    action = h.Action,
                    actor = h.Actor,
                    details = h.Details,
                    timestamp = h.Timestamp.ToString("dd/MM/yyyy HH:mm")
                })
                .ToListAsync();

            var now = DateTime.Now;
            string deadlineState = "NORMAL";
            if (task.Status != "COMPLETED" && task.Status != "CANCELLED")
            {
                if (task.Deadline < now) deadlineState = "OVERDUE";
                else if ((task.Deadline - now).TotalHours <= 24) deadlineState = "NEAR_DUE";
            }

            return Json(new {
                task = new {
                    id = task.Id,
                    taskCode = task.TaskCode,
                    title = task.Title,
                    description = task.Description,
                    relatedModule = task.RelatedModule,
                    relatedRecordCode = task.RelatedRecordCode,
                    assignee = task.AssigneeNameSnapshot,
                    priority = task.Priority,
                    status = task.Status,
                    progress = task.ProgressPercent,
                    deadline = task.Deadline.ToString("dd/MM/yyyy HH:mm"),
                    deadlineState = deadlineState,
                    createdAt = task.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    createdBy = task.CreatedBy,
                    reminderEnabled = task.ReminderEnabled
                },
                history = history
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTask([FromBody] HrTask model)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                return Json(new { success = false, message = "Tiêu đề không được để trống" });

            var emp = await _context.Employees.FindAsync(model.AssigneeEmployeeId);
            if (emp == null)
                return Json(new { success = false, message = "Người phụ trách không hợp lệ" });

            model.AssigneeNameSnapshot = emp.Name;
            model.TaskCode = "TSK-" + DateTime.Now.ToString("yyyyMMdd") + "-" + (new Random().Next(1000, 9999));
            model.Status = "OPEN";
            model.CreatedAt = DateTime.Now;
            model.UpdatedAt = DateTime.Now;
            model.CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            _context.HrTasks.Add(model);
            await _context.SaveChangesAsync();

            _context.HrTaskHistories.Add(new HrTaskHistory {
                HrTaskId = model.Id,
                Action = "CREATE_TASK",
                Actor = model.CreatedBy,
                Details = "Tạo công việc mới"
            });
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProgress([FromBody] dynamic data)
        {
            int id = data.id;
            int progress = data.progress;
            
            if (progress < 0 || progress > 100)
                return Json(new { success = false, message = "Tiến độ không hợp lệ" });

            var task = await _context.HrTasks.FindAsync(id);
            if (task == null) return NotFound();

            task.ProgressPercent = progress;
            task.UpdatedAt = DateTime.Now;
            
            if (progress == 100 && task.Status != "COMPLETED")
            {
                task.Status = "COMPLETED";
                task.CompletedAt = DateTime.Now;
                task.CompletedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
            }
            else if (progress > 0 && progress < 100 && task.Status == "OPEN")
            {
                task.Status = "IN_PROGRESS";
            }

            _context.HrTasks.Update(task);
            
            _context.HrTaskHistories.Add(new HrTaskHistory {
                HrTaskId = id,
                Action = "UPDATE_PROGRESS",
                Actor = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                Details = $"Cập nhật tiến độ lên {progress}%"
            });
            
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus([FromBody] dynamic data)
        {
            int id = data.id;
            string status = data.status;

            if (status != "OPEN" && status != "IN_PROGRESS" && status != "COMPLETED" && status != "CANCELLED")
                return BadRequest(new { success = false, message = "Trạng thái không hợp lệ" });

            var task = await _context.HrTasks.FindAsync(id);
            if (task == null) return NotFound();

            if (task.Status == "COMPLETED" || task.Status == "CANCELLED")
                return Json(new { success = false, message = "Không thể đổi trạng thái công việc đã kết thúc." });

            if (task.Status == "OPEN" && status == "IN_PROGRESS") { }
            else if (task.Status == "IN_PROGRESS" && (status == "OPEN" || status == "IN_PROGRESS")) { }
            else if (status != "COMPLETED" && status != "CANCELLED")
                return Json(new { success = false, message = "Chuyển trạng thái không hợp lệ." });

            task.Status = status;
            task.UpdatedAt = DateTime.Now;

            if (status == "COMPLETED") {
                task.ProgressPercent = 100;
                task.CompletedAt = DateTime.Now;
                task.CompletedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
            }
            else if (status == "CANCELLED") {
                task.CancelledAt = DateTime.Now;
                task.CancelledBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
            }

            _context.HrTasks.Update(task);
            
            _context.HrTaskHistories.Add(new HrTaskHistory {
                HrTaskId = id,
                Action = "CHANGE_STATUS",
                Actor = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                Details = $"Đổi trạng thái thành {status}"
            });
            
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExtendDeadline([FromBody] dynamic data)
        {
            int id = data.id;
            DateTime newDeadline = data.deadline;
            string reason = data.reason;

            var task = await _context.HrTasks.FindAsync(id);
            if (task == null) return NotFound();

            if (task.Status == "COMPLETED" || task.Status == "CANCELLED")
                return Json(new { success = false, message = "Không thể gia hạn công việc đã kết thúc." });
            if (newDeadline <= task.Deadline)
                return Json(new { success = false, message = "Deadline mới phải sau deadline hiện tại." });

            string oldDeadline = task.Deadline.ToString("dd/MM/yyyy HH:mm");
            task.Deadline = newDeadline;
            task.UpdatedAt = DateTime.Now;

            _context.HrTasks.Update(task);
            
            _context.HrTaskHistories.Add(new HrTaskHistory {
                HrTaskId = id,
                Action = "EXTEND_DEADLINE",
                Actor = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                Details = $"Gia hạn deadline từ {oldDeadline} sang {newDeadline.ToString("dd/MM/yyyy HH:mm")}. Lý do: {reason}"
            });
            
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
