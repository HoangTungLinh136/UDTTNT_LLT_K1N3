using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    public class RewardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly Services.IApprovalEngine _approvalEngine;
        private readonly Services.RewardWorkflowPolicy _rewardPolicy;
        private readonly Services.IProtectedFileService _protectedFileService;
        private readonly Services.NotificationEngineService _notificationEngine;

        public RewardController(
            ApplicationDbContext context, 
            Services.IApprovalEngine approvalEngine, 
            Services.RewardWorkflowPolicy rewardPolicy,
            Services.IProtectedFileService protectedFileService,
            Services.NotificationEngineService notificationEngine)
        {
            _context = context;
            _approvalEngine = approvalEngine;
            _rewardPolicy = rewardPolicy;
            _protectedFileService = protectedFileService;
            _notificationEngine = notificationEngine;
        }

        private bool CheckLogin()
        {
            var user = HttpContext.Session.GetString("User");
            return !string.IsNullOrEmpty(user);
        }

        private string GetCurrentUser()
        {
            return CurrentActor.Username(User) ?? HttpContext.Session.GetString("User") ?? "System";
        }

        private Employee? GetCurrentEmployee()
        {
            var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(username)) return null;
            var employeeId = _context.Users.AsNoTracking()
                .Where(u => u.Username == username && u.CanLogin)
                .Select(u => u.EmployeeId)
                .SingleOrDefault();
            return employeeId.HasValue ? _context.Employees.Find(employeeId.Value) : null;
        }

        private async Task QueueLeaderNotificationsAsync(RewardRecord record)
        {
            var now = DateTime.Now;
            var leaderIds = await _context.LeaderAssignments
                .Where(a => a.OrganizationalUnit == record.DepartmentSnapshot && a.EffectiveFrom <= now && (a.EffectiveTo == null || a.EffectiveTo > now))
                .Select(a => a.UserId).Distinct().ToListAsync();
            var leaders = await _context.Users.Where(u => leaderIds.Contains(u.Id) && u.Role == AppRoles.Leader && u.CanLogin).Select(u => new { u.Id, u.Username }).ToListAsync();
            foreach (var leader in leaders)
                _notificationEngine.QueueEvent(_context, "REWARD_PENDING_LEADER", new { RewardId = record.Id, EmployeeName = record.EmployeeNameSnapshot }, leader.Username, null, $"reward:{record.Id}:pending-leader:{leader.Id}");
        }

        private void QueueProposerNotification(RewardRecord record, string eventCode)
        {
            var proposer = record.SubmittedBy ?? record.ProposedBy ?? record.CreatedBy;
            if (!string.IsNullOrWhiteSpace(proposer))
                _notificationEngine.QueueEvent(_context, eventCode, new { RewardId = record.Id, EmployeeName = record.EmployeeNameSnapshot, DecisionNumber = record.DecisionNumber, RejectionReason = record.RejectionReason }, proposer, null, $"reward:{record.Id}:{eventCode}:{proposer}");
        }

        // 1. INDEX — Danh sách khen thưởng
        public async Task<IActionResult> Index(
            string? searchName,
            string? department,
            string? status,
            string? rewardType,
            int page = 1,
            int pageSize = 10)
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            
            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            if (role == "Employee")
            {
                return View("MyIndex");
            }

            var scopedQuery = _context.RewardRecords
                .Include(r => r.Employee)
                .AsNoTracking()
                .AsQueryable();

            if (role == AppRoles.Manager)
            {
                var managerScope = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.IManagerScopeService>();
                var allowedDepts = managerScope.GetManagedOrganizationalUnits();
                scopedQuery = scopedQuery.Where(r => r.DepartmentSnapshot != null && allowedDepts.Contains(r.DepartmentSnapshot));
            }
            else if (role == AppRoles.Leader)
            {
                var leaderScope = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.ILeadershipScopeService>();
                var allowedDepts = leaderScope.GetOverseenOrganizationalUnits();
                scopedQuery = scopedQuery.Where(r => r.DepartmentSnapshot != null && allowedDepts.Contains(r.DepartmentSnapshot));
            }

            var query = scopedQuery;

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                query = query.Where(r => 
                    (r.EmployeeNameSnapshot != null && r.EmployeeNameSnapshot.Contains(searchName)) ||
                    (r.EmployeeCodeSnapshot != null && r.EmployeeCodeSnapshot.Contains(searchName)) ||
                    (r.Achievement != null && r.Achievement.Contains(searchName)));
            }

            if (!string.IsNullOrWhiteSpace(department))
            {
                query = query.Where(r => r.DepartmentSnapshot == department);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.ApprovalStatus == status);
            }

            if (!string.IsNullOrWhiteSpace(rewardType))
            {
                query = query.Where(r => r.RewardType == rewardType);
            }

            page = Math.Max(1, page);
            pageSize = pageSize < 5 || pageSize > 100 ? 10 : pageSize;
            var totalItems = await query.CountAsync();
            var approvedAmount = await query.Where(r => r.ApprovalStatus == "APPROVED" && r.HasMonetaryReward)
                .Select(r => (double?)r.RewardAmount).SumAsync() ?? 0d;
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages > 0 && page > totalPages) page = totalPages;
            var records = await query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Calculate Statistics
            ViewBag.StatNew = await scopedQuery.CountAsync(r => r.ApprovalStatus == "DRAFT");
            ViewBag.StatPending = await scopedQuery.CountAsync(r => r.ApprovalStatus == "PENDING_APPROVAL" || r.ApprovalStatus == "PENDING_LEADER");
            ViewBag.StatApproved = await scopedQuery.CountAsync(r => r.ApprovalStatus == "APPROVED");
            
            // Tổng giá trị thưởng của các hồ sơ đã duyệt (trong phạm vi bộ lọc query)
            ViewBag.StatTotalAmount = approvedAmount;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageNumber = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;

            // Departments for Filter
            var departmentQuery = _context.Employees.AsNoTracking()
                .Where(e => !string.IsNullOrEmpty(e.Department))
                .Select(e => e.Department!);
            if (role == AppRoles.Manager)
            {
                var managerScope = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.IManagerScopeService>();
                var allowedDepts = managerScope.GetManagedOrganizationalUnits();
                departmentQuery = departmentQuery.Where(d => allowedDepts.Contains(d));
            }
            ViewBag.Departments = await departmentQuery.Distinct().ToListAsync();

            // RewardTypes for Filter (Lấy danh sách các loại thưởng hiện có trong DB)
            ViewBag.RewardTypes = new List<string> {
                "Thành tích xuất sắc",
                "Gương mặt tiêu biểu",
                "Sáng kiến đổi mới",
                "Đạt doanh số",
                "Khác"
            };

            ViewBag.CurrentSearch = searchName;
            ViewBag.CurrentDept = department;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentRewardType = rewardType;
            ViewBag.CurrentUser = GetCurrentUser();
            ViewBag.CurrentRole = role;

            return View(records);
        }

        // 2. DETAIL — Lấy chi tiết
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            if (!CheckLogin()) return Unauthorized();

            var record = await _context.RewardRecords
                .Include(r => r.Histories)
                .Include(r => r.Employee)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (record == null) return NotFound();

            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            if (role == "Employee")
            {
                var emp = GetCurrentEmployee();
                if (emp == null || record.EmployeeId != emp.Id)
                    return Forbid();
            }
            else if (role == AppRoles.Manager)
            {
                var managerScope = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.IManagerScopeService>();
                if (!managerScope.CanAccessDepartment(record.DepartmentSnapshot)) return Forbid();
            }
            else if (role == AppRoles.Leader)
            {
                var leaderScope = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.ILeadershipScopeService>();
                if (!leaderScope.CanAccessDepartment(record.DepartmentSnapshot)) return Forbid();
            }

            // Sắp xếp lịch sử
            record.Histories = record.Histories.OrderByDescending(h => h.Timestamp).ToList();

            return Json(new { success = true, data = record });
        }

        // 4. API LẤY DANH SÁCH NHÂN VIÊN (Dùng cho Tự động điền)
        [HttpGet]
        [Authorize(Roles = $"{AppRoles.Manager},{AppRoles.HR},{AppRoles.Leader}")]
        public async Task<IActionResult> SearchEmployee(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return Json(new List<object>());

            var employeesQuery = _context.Employees
                .Where(e => e.Name.Contains(query) || e.EmployeeCode.Contains(query))
                .Where(e => e.Position != "Đã nghỉ"); // Không hiển thị NV đã nghỉ

            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            if (role == AppRoles.Manager)
            {
                var managerScope = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.IManagerScopeService>();
                var allowedDepts = managerScope.GetManagedOrganizationalUnits();
                employeesQuery = employeesQuery.Where(e => e.Department != null && allowedDepts.Contains(e.Department));
            }
            else if (role == AppRoles.Leader)
            {
                var leaderScope = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.ILeadershipScopeService>();
                var allowedDepts = leaderScope.GetOverseenOrganizationalUnits();
                employeesQuery = employeesQuery.Where(e => e.Department != null && allowedDepts.Contains(e.Department));
            }

            var employees = await employeesQuery
                .Select(e => new {
                    e.Id,
                    e.EmployeeCode,
                    e.Name,
                    e.Department,
                    e.Position,
                    e.JobTitle
                })
                .Take(10)
                .ToListAsync();

            return Json(employees);
        }

        // 4. CREATE — Tạo mới (Lưu nháp hoặc Trình duyệt)
        [HttpPost]
        [Authorize(Roles = AppRoles.Manager)]
        public async Task<IActionResult> Create(RewardRecord model, string actionType)
        {
            if (!CheckLogin()) return Unauthorized();

            var employee = await _context.Employees.FindAsync(model.EmployeeId);
            if (employee == null) return Json(new { success = false, message = "Nhân viên không tồn tại." });

            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            if (role == AppRoles.Manager)
            {
                var managerScope = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.IManagerScopeService>();
                if (!managerScope.CanAccessDepartment(employee.Department)) return Forbid();
            }

            if (model.HasMonetaryReward)
            {
                if (model.RewardAmount <= 0) return Json(new { success = false, message = "Giá trị thưởng phải lớn hơn 0." });
                if (string.IsNullOrWhiteSpace(model.PayrollPeriod)) return Json(new { success = false, message = "Vui lòng chọn kỳ lương." });
            }
            else
            {
                model.RewardAmount = 0;
                model.PayrollPeriod = null;
            }

            model.EmployeeCodeSnapshot = employee.EmployeeCode;
            model.EmployeeNameSnapshot = employee.Name;
            model.DepartmentSnapshot = employee.Department;
            model.PositionSnapshot = employee.Position;

            model.CreatedBy = GetCurrentUser();
            model.CreatedAt = DateTime.Now;
            model.ProposedBy = GetCurrentUser(); // Người đang login

            if (actionType == "SUBMIT")
            {
                model.ApprovalStatus = "PENDING_APPROVAL";
                model.SubmittedBy = GetCurrentUser();
                model.SubmittedAt = DateTime.Now;
            }
            else
            {
                model.ApprovalStatus = "DRAFT";
            }

            _context.RewardRecords.Add(model);
            await _context.SaveChangesAsync();

            // Log history
            var history = new RewardHistory
            {
                RewardRecordId = model.Id,
                Actor = GetCurrentUser(),
                Action = "CREATE",
                Details = actionType == "SUBMIT" ? "Tạo mới và trình duyệt đề xuất khen thưởng" : "Tạo mới bản nháp khen thưởng"
            };
            _context.RewardHistories.Add(history);
            await _context.SaveChangesAsync();

            if (actionType == "SUBMIT")
            {
                _notificationEngine.QueueEvent(_context, "REWARD_SUBMITTED", new { 
                    RewardId = model.Id, 
                    EmployeeName = model.EmployeeNameSnapshot 
                }, null, null, "REW_SUBMIT_" + model.Id);
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        // 5. UPDATE — Cập nhật bản nháp
        [HttpPost]
        [Authorize(Roles = AppRoles.Manager)]
        public async Task<IActionResult> Update(RewardRecord model, string actionType)
        {
            if (!CheckLogin()) return Unauthorized();

            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var record = await _context.RewardRecords.FindAsync(model.Id);
                if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ." });
                await _context.Entry(record).ReloadAsync();

            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            if (role == AppRoles.Manager)
            {
                if (!string.Equals(record.CreatedBy, GetCurrentUser(), StringComparison.OrdinalIgnoreCase)) return Forbid();
                var managerScope = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.IManagerScopeService>();
                if (!managerScope.CanAccessDepartment(record.DepartmentSnapshot)) return Forbid();
            }

            if (record.ApprovalStatus != "DRAFT" && record.ApprovalStatus != "REJECTED") 
                return Json(new { success = false, message = "Chỉ được phép sửa bản nháp hoặc hồ sơ bị từ chối." });

            if (model.HasMonetaryReward)
            {
                if (model.RewardAmount <= 0) return Json(new { success = false, message = "Giá trị thưởng phải lớn hơn 0." });
                if (string.IsNullOrWhiteSpace(model.PayrollPeriod)) return Json(new { success = false, message = "Vui lòng chọn kỳ lương." });
            }
            else
            {
                model.RewardAmount = 0;
                model.PayrollPeriod = null;
            }

            record.RewardType = model.RewardType;
            record.Title = model.Title;
            record.Achievement = model.Achievement;
            record.Description = model.Description;
            record.ProposedDate = model.ProposedDate;
            record.EffectiveDate = model.EffectiveDate;
            record.HasMonetaryReward = model.HasMonetaryReward;
            record.RewardAmount = model.RewardAmount;
            record.PayrollPeriod = model.PayrollPeriod;

            if (actionType == "SUBMIT")
            {
                record.ApprovalStatus = "PENDING_APPROVAL";
                record.SubmittedBy = GetCurrentUser();
                record.SubmittedAt = DateTime.Now;

                _notificationEngine.QueueEvent(_context, "REWARD_SUBMITTED", new { 
                    RewardId = record.Id, 
                    EmployeeName = record.EmployeeNameSnapshot 
                }, null, null, "REW_SUBMIT_" + record.Id);
            }

            // Log history
            var history = new RewardHistory
            {
                RewardRecordId = record.Id,
                Actor = GetCurrentUser(),
                Action = actionType == "SUBMIT" ? "SUBMIT" : "UPDATE",
                Details = actionType == "SUBMIT" ? "Trình duyệt hồ sơ khen thưởng" : "Cập nhật bản nháp"
            };
            _context.RewardHistories.Add(history);
            
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Json(new { success = true });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                return Json(new { success = false, message = "Dữ liệu đã bị thay đổi bởi người khác. Vui lòng tải lại trang." });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Đã xảy ra lỗi trong quá trình xử lý." });
            }
        }

        // 6. APPROVE — Duyệt
        [HttpPost]
        [Authorize(Roles = $"{AppRoles.HR},{AppRoles.Leader}")]
        public async Task<IActionResult> Approve(int id)
        {
            if (!CheckLogin()) return Unauthorized();

            var record = await _context.RewardRecords.FindAsync(id);
            if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role") ?? "";
            var result = await _approvalEngine.ProcessDecisionAsync(
                record, "APPROVE", GetCurrentUser(), role, null, _rewardPolicy);

            if (result.Success)
            {
                var history = new RewardHistory
                {
                    RewardRecordId = record.Id,
                    Actor = GetCurrentUser(),
                    Action = "APPROVE",
                    Details = "Phê duyệt hồ sơ khen thưởng"
                };
                _context.RewardHistories.Add(history);
                await _context.SaveChangesAsync();
                if (role == AppRoles.HR && record.ApprovalStatus == "PENDING_LEADER") await QueueLeaderNotificationsAsync(record);
                if (role == AppRoles.Leader && record.ApprovalStatus == "APPROVED") QueueProposerNotification(record, "REWARD_APPROVED");
                await _context.SaveChangesAsync();
            }

            return Json(new { success = result.Success, message = result.Message });
        }

        // 7. REJECT — Từ chối
        [HttpPost]
        [Authorize(Roles = $"{AppRoles.HR},{AppRoles.Leader}")]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            if (!CheckLogin()) return Unauthorized();

            if (string.IsNullOrWhiteSpace(reason))
                return Json(new { success = false, message = "Vui lòng nhập lý do từ chối." });

            var record = await _context.RewardRecords.FindAsync(id);
            if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role") ?? "";
            var result = await _approvalEngine.ProcessDecisionAsync(
                record, "REJECT", GetCurrentUser(), role, reason, _rewardPolicy);

            if (result.Success)
            {
                var history = new RewardHistory
                {
                    RewardRecordId = record.Id,
                    Actor = GetCurrentUser(),
                    Action = "REJECT",
                    Details = $"Từ chối: {reason}"
                };
                _context.RewardHistories.Add(history);
                await _context.SaveChangesAsync();
                if (role == AppRoles.Leader && record.ApprovalStatus == "REJECTED") QueueProposerNotification(record, "REWARD_REJECTED");
                await _context.SaveChangesAsync();
            }

            return Json(new { success = result.Success, message = result.Message });
        }

        // 8. CANCEL — Hủy
        [HttpPost]
        [Authorize(Roles = AppRoles.Manager)]
        public async Task<IActionResult> Cancel(int id, string reason)
        {
            if (!CheckLogin()) return Unauthorized();

            if (string.IsNullOrWhiteSpace(reason))
                return Json(new { success = false, message = "Vui lòng nhập lý do hủy." });

            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var record = await _context.RewardRecords.FindAsync(id);
                if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ." });
                await _context.Entry(record).ReloadAsync();

            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            if (role == AppRoles.Manager)
            {
                if (!string.Equals(record.CreatedBy, GetCurrentUser(), StringComparison.OrdinalIgnoreCase)) return Forbid();
                var managerScope = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.IManagerScopeService>();
                if (!managerScope.CanAccessDepartment(record.DepartmentSnapshot)) return Forbid();
            }

            // Nếu đã đồng bộ lương rồi thì cảnh báo
            if (record.IsPayrollSynced)
                return Json(new { success = false, message = "Hồ sơ này đã được tính lương (Payroll Synced). Không thể hủy bằng cách thông thường." });

            record.ApprovalStatus = "CANCELLED";
            record.CancelledBy = GetCurrentUser();
            record.CancelledAt = DateTime.Now;
            record.CancellationReason = reason;

            var history = new RewardHistory
            {
                RewardRecordId = record.Id,
                Actor = GetCurrentUser(),
                Action = "CANCEL",
                Details = $"Hủy hồ sơ: {reason}"
            };
            _context.RewardHistories.Add(history);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Json(new { success = true });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                return Json(new { success = false, message = "Dữ liệu đã bị thay đổi bởi người khác. Vui lòng tải lại trang." });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Đã xảy ra lỗi trong quá trình hủy." });
            }
        }

        // --- BỔ SUNG CÁC API CHO EMPLOYEE SELF-SERVICE ---

        [HttpGet]
        public async Task<IActionResult> GetMySummary()
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = GetCurrentEmployee();
            if (emp == null) return Unauthorized();

            var records = await _context.RewardRecords
                .Where(r => r.EmployeeId == emp.Id && (r.ApprovalStatus == "APPROVED" || r.ApprovalStatus == "ISSUED"))
                .ToListAsync();

            var totalCount = records.Count;
            var currentYearCount = records.Count(r => (r.EffectiveDate ?? r.CreatedAt).Year == DateTime.Now.Year);
            var totalAmount = records.Where(r => r.HasMonetaryReward).Sum(r => r.RewardAmount);
            
            // Tìm khen thưởng nổi bật (Featured - Ưu tiên Thành tích xuất sắc hoặc Gương mặt tiêu biểu, hoặc khen thưởng giá trị cao nhất)
            var featuredCount = records.Count(r => r.RewardType == "Thành tích xuất sắc" || r.RewardType == "Gương mặt tiêu biểu");

            return Json(new { totalCount, currentYearCount, totalAmount, featuredCount });
        }

        [HttpGet]
        public async Task<IActionResult> GetFeaturedReward()
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = GetCurrentEmployee();
            if (emp == null) return Unauthorized();

            var records = await _context.RewardRecords
                .Where(r => r.EmployeeId == emp.Id && (r.ApprovalStatus == "APPROVED" || r.ApprovalStatus == "ISSUED"))
                .OrderByDescending(r => r.EffectiveDate ?? r.CreatedAt)
                .ToListAsync();
                
            var featured = records.OrderByDescending(r => r.HasMonetaryReward ? r.RewardAmount : 0)
                                  .ThenByDescending(r => r.EffectiveDate ?? r.CreatedAt)
                                  .FirstOrDefault();

            if (featured == null) return Json(null);

            return Json(new {
                featured.Id,
                featured.Title,
                featured.Achievement,
                featured.Description,
                featured.RewardType,
                Date = featured.EffectiveDate ?? featured.CreatedAt,
                featured.HasMonetaryReward,
                featured.RewardAmount
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetMyRewards()
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = GetCurrentEmployee();
            if (emp == null) return Unauthorized();

            var records = await _context.RewardRecords
                .Where(r => r.EmployeeId == emp.Id && (r.ApprovalStatus == "APPROVED" || r.ApprovalStatus == "ISSUED"))
                .OrderByDescending(r => r.EffectiveDate ?? r.CreatedAt)
                .Select(r => new {
                    r.Id,
                    r.Title,
                    r.RewardType,
                    r.Achievement,
                    Date = r.EffectiveDate ?? r.CreatedAt,
                    r.HasMonetaryReward,
                    r.RewardAmount,
                    r.DecisionNumber,
                    HasDocument = !string.IsNullOrEmpty(r.DecisionDocumentUrl)
                })
                .ToListAsync();

            return Json(records);
        }

        [HttpGet]
        public async Task<IActionResult> GetRewardDetail(int id)
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = GetCurrentEmployee();
            if (emp == null) return Unauthorized();

            var r = await _context.RewardRecords
                .FirstOrDefaultAsync(x => x.Id == id && x.EmployeeId == emp.Id);
                
            if (r == null || (r.ApprovalStatus != "APPROVED" && r.ApprovalStatus != "ISSUED")) 
                return NotFound("Không tìm thấy thông tin khen thưởng.");

            return Json(new {
                r.Id,
                r.Title,
                r.RewardType,
                r.Achievement,
                r.Description,
                Date = r.EffectiveDate ?? r.CreatedAt,
                r.HasMonetaryReward,
                r.RewardAmount,
                r.DecisionNumber,
                HasDocument = !string.IsNullOrEmpty(r.DecisionDocumentUrl)
            });
        }

        [HttpGet]
        public async Task<IActionResult> DownloadDecision(int id)
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = GetCurrentEmployee();
            
            // Scope check: must be owner, or HR, or Leader with scope
            var r = await _context.RewardRecords.FirstOrDefaultAsync(x => x.Id == id);
            if (r == null || (r.ApprovalStatus != "APPROVED" && r.ApprovalStatus != "ISSUED") || string.IsNullOrEmpty(r.DecisionDocumentUrl))
                return NotFound("Không tìm thấy quyết định khen thưởng.");

            var role = CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role");
            if (role == AppRoles.Employee)
            {
                if (emp == null || r.EmployeeId != emp.Id) return Unauthorized();
            }
            else if (role == AppRoles.Leader)
            {
                var leaderScope = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.ILeadershipScopeService>();
                if (!leaderScope.CanAccessDepartment(r.DepartmentSnapshot)) return Forbid();
            }
            else if (role != AppRoles.HR)
            {
                return Forbid();
            }

            try
            {
                var fileData = await _protectedFileService.GetProtectedFileAsync(r.DecisionDocumentUrl);
                var fileName = $"QuyetDinhKhenThuong_{r.EmployeeCodeSnapshot}_{DateTime.Now:yyyyMMdd}.pdf";
                return PhysicalFile(fileData.PhysicalPath, fileData.ContentType, fileName);
            }
            catch (Exception)
            {
                return NotFound("Không thể tải tập tin quyết định.");
            }
        }
    }
}
