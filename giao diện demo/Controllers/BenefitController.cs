using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    public class BenefitController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IApprovalEngine _approvalEngine;
        private readonly IWorkflowPolicy<BenefitEnrollment> _policy;
        private readonly IAuditLogService _auditLog;

        public BenefitController(
            ApplicationDbContext context,
            IApprovalEngine approvalEngine,
            IWorkflowPolicy<BenefitEnrollment> policy,
            IAuditLogService auditLog)
        {
            _context = context;
            _approvalEngine = approvalEngine;
            _policy = policy;
            _auditLog = auditLog;
        }

        private bool CheckLogin()
        {
            var user = HttpContext.Session.GetString("User");
            return !string.IsNullOrEmpty(user);
        }

        private async Task<Employee?> GetCurrentEmployeeAsync()
        {
            var user = await CurrentActor.ResolveUserAsync(User, _context);
            if (user?.EmployeeId == null)
            {
                var username = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? HttpContext.Session.GetString("User");
                if (string.IsNullOrEmpty(username)) return null;
                user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username && u.CanLogin);
                if (user?.EmployeeId == null) return null;
            }
            return await _context.Employees.FindAsync(user.EmployeeId.Value);
        }

        private int GetCurrentEmployeeId()
        {
            var emp = GetCurrentEmployeeAsync().GetAwaiter().GetResult();
            return emp?.Id ?? (HttpContext.Session.GetInt32("EmployeeId") ?? 0);
        }

        private string GetCurrentUsername()
        {
            return CurrentActor.Username(User) ?? HttpContext.Session.GetString("User") ?? "SYSTEM";
        }

        private string GetCurrentRole()
        {
            return CurrentActor.Role(User) ?? HttpContext.Session.GetString("Role") ?? "Employee";
        }

        // --- 1. TRANG CHỦ PHÚC LỢI ---
        public IActionResult Index()
        {
            if (!CheckLogin()) return RedirectToAction("Login", "Account");
            return View();
        }

        [HttpGet]
        [Authorize(Policy = AppPolicies.HROnly)]
        public IActionResult HrIndex() => View("HrIndex");

        [HttpGet]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> GetHrEnrollments(string? search, string? status)
        {
            var q = _context.BenefitEnrollments.Include(e => e.Program).Include(e => e.Employee).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim(); q = q.Where(e => (e.EmployeeNameSnapshot != null && e.EmployeeNameSnapshot.Contains(s)) || (e.Program.Name != null && e.Program.Name.Contains(s))); }
            if (!string.IsNullOrWhiteSpace(status)) q = q.Where(e => e.ApprovalStatus == status);
            return Json(await q.OrderByDescending(e => e.RegistrationDate).Take(200).Select(e => new { e.Id, employee = e.Employee != null ? e.Employee.Name : "", program = e.Program.Name, e.RegistrationDate, status = e.ApprovalStatus, entitlement = _context.BenefitEntitlements.Any(x => x.EmployeeId == e.EmployeeId && x.BenefitProgramId == e.BenefitProgramId && x.Status == "Active") }).ToListAsync());
        }

        // --- 2. API: LẤY THÔNG TIN SUMMARY (KPIs) ---
        [HttpGet]
        public async Task<IActionResult> GetMyBenefitSummary()
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Forbid();
            var empId = emp.Id;

            var activeBenefits = await _context.BenefitEntitlements
                .Where(b => b.EmployeeId == empId && b.Status == "Active")
                .Include(b => b.Program)
                .ToListAsync();

            var activeInsuranceCount = activeBenefits.Count(b => b.Program?.BenefitType == "Insurance");
            var activeBenefitCount = activeBenefits.Count(b => b.Program?.BenefitType != "Insurance");

            var now = DateTime.Now;
            var availablePrograms = await _context.BenefitPrograms
                .Where(p => p.Status == "Open" && p.RegistrationStart <= now && p.RegistrationEnd >= now && !p.IsAutoApplied)
                .ToListAsync();
            
            var myEnrollments = await _context.BenefitEnrollments
                .Where(e => e.EmployeeId == empId)
                .ToListAsync();

            int eligibleProgramCount = 0;
            foreach (var prog in availablePrograms)
            {
                var enrolled = myEnrollments.Any(e => e.BenefitProgramId == prog.Id && (e.ApprovalStatus == "PENDING_APPROVAL" || e.ApprovalStatus == "APPROVED"));
                if (!enrolled) eligibleProgramCount++;
            }

            var pendingEnrollmentCount = myEnrollments.Count(e => e.ApprovalStatus == "PENDING_APPROVAL");

            return Json(new {
                activeInsuranceCount,
                activeBenefitCount,
                eligibleProgramCount,
                pendingEnrollmentCount
            });
        }

        // --- 3. API: LẤY QUYỀN LỢI ĐANG HƯỞNG ---
        [HttpGet]
        public async Task<IActionResult> GetMyActiveBenefits()
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Forbid();
            var empId = emp.Id;

            var activeBenefits = await _context.BenefitEntitlements
                .Where(b => b.EmployeeId == empId && b.Status == "Active")
                .Include(b => b.Program)
                .OrderByDescending(b => b.EffectiveFrom)
                .Select(b => new {
                    b.Id,
                    programName = b.Program != null ? b.Program.Name : "",
                    benefitType = b.Program != null ? b.Program.BenefitType : "",
                    b.EffectiveFrom,
                    b.EffectiveTo,
                    b.Status,
                    b.Amount,
                    b.Notes
                })
                .ToListAsync();

            return Json(activeBenefits);
        }

        // --- 4. API: LẤY DANH SÁCH CHƯƠNG TRÌNH PHÚC LỢI ---
        [HttpGet]
        public async Task<IActionResult> GetAvailablePrograms()
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Forbid();
            var empId = emp.Id;

            var allPrograms = await _context.BenefitPrograms
                .Where(p => p.Status != "Draft")
                .OrderByDescending(p => p.Id)
                .ToListAsync();

            var myEnrollments = await _context.BenefitEnrollments
                .Where(e => e.EmployeeId == empId)
                .ToListAsync();

            var now = DateTime.Now;

            var result = allPrograms.Select(p => {
                var enrollment = myEnrollments.FirstOrDefault(e => e.BenefitProgramId == p.Id);
                var isWindowOpen = p.RegistrationStart <= now && p.RegistrationEnd >= now;
                var isEligible = isWindowOpen && p.Status == "Open" && !p.IsAutoApplied;
                
                string enrollmentStatus = "NOT_REGISTERED";
                if (enrollment != null) enrollmentStatus = enrollment.ApprovalStatus;
                
                if (enrollmentStatus == "PENDING_APPROVAL" || enrollmentStatus == "APPROVED")
                {
                    isEligible = false;
                }

                return new {
                    id = p.Id,
                    name = p.Name,
                    description = p.Description,
                    benefitType = p.BenefitType,
                    status = p.Status,
                    isAutoApplied = p.IsAutoApplied,
                    registrationStart = p.RegistrationStart,
                    registrationEnd = p.RegistrationEnd,
                    enrollmentStatus = enrollmentStatus,
                    isEligible = isEligible,
                    eligibilityMessage = isEligible ? "" : (!isWindowOpen ? "Ngoài thời gian đăng ký" : (enrollment != null ? "Đã đăng ký" : "Không đủ điều kiện"))
                };
            });

            return Json(result);
        }

        // --- 5. API: ĐĂNG KÝ CHƯƠNG TRÌNH ---
        [HttpPost]
        public async Task<IActionResult> Register(int programId, string? notes)
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Forbid();
            var empId = emp.Id;

            var program = await _context.BenefitPrograms.FindAsync(programId);
            if (program == null || program.Status != "Open" || program.IsAutoApplied)
            {
                return BadRequest(new { message = "Chương trình không hợp lệ hoặc không thể đăng ký." });
            }

            var now = DateTime.Now;
            if (now < program.RegistrationStart || now > program.RegistrationEnd)
            {
                return BadRequest(new { message = "Hiện không trong thời gian đăng ký." });
            }

            var existing = await _context.BenefitEnrollments
                .FirstOrDefaultAsync(e => e.BenefitProgramId == programId && e.EmployeeId == empId && (e.ApprovalStatus == "PENDING_APPROVAL" || e.ApprovalStatus == "APPROVED"));
            
            if (existing != null)
            {
                return BadRequest(new { message = "Bạn đã đăng ký chương trình này rồi." });
            }

            if (program.Capacity.HasValue)
            {
                var count = await _context.BenefitEnrollments.CountAsync(e => e.BenefitProgramId == programId && e.ApprovalStatus != "CANCELLED" && e.ApprovalStatus != "REJECTED");
                if (count >= program.Capacity.Value)
                    return BadRequest(new { message = "Chương trình đã đủ số lượng tham gia." });
            }

            var enrollment = new BenefitEnrollment
            {
                EmployeeId = empId,
                EmployeeNameSnapshot = emp.Name,
                BenefitProgramId = programId,
                RegistrationDate = now,
                Notes = notes,
                ApprovalStatus = program.RequiresApproval ? "PENDING_APPROVAL" : "APPROVED"
            };

            _context.BenefitEnrollments.Add(enrollment);

            if (!program.RequiresApproval)
            {
                // Auto-create entitlement when no approval required
                var entitlement = new BenefitEntitlement
                {
                    EmployeeId = empId,
                    EmployeeNameSnapshot = emp.Name,
                    BenefitProgramId = programId,
                    EffectiveFrom = now,
                    Status = "Active"
                };
                _context.BenefitEntitlements.Add(entitlement);
            }

            await _context.SaveChangesAsync();
            await _auditLog.LogAsync("RegisterBenefit", "Benefit", "BenefitCore", enrollment.Id.ToString(), "Enrollment", "Success");

            return Ok(new { success = true, message = "Đăng ký thành công!" });
        }

        // --- 6. API: LỊCH SỬ ĐĂNG KÝ ---
        [HttpGet]
        public async Task<IActionResult> GetMyEnrollmentHistory()
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Forbid();
            var empId = emp.Id;

            var history = await _context.BenefitEnrollments
                .Where(e => e.EmployeeId == empId)
                .Include(e => e.Program)
                .OrderByDescending(e => e.RegistrationDate)
                .ToListAsync();

            return Json(history.Select(e => new {
                id = e.Id,
                programName = e.Program?.Name,
                registrationDate = e.RegistrationDate,
                status = e.ApprovalStatus,
                notes = e.Notes
            }));
        }

        // --- 7. API: CHI TIẾT QUYỀN LỢI ĐANG HƯỞNG ---
        [HttpGet]
        public async Task<IActionResult> GetBenefitDetail(int id)
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Forbid();
            var empId = emp.Id;

            var benefit = await _context.BenefitEntitlements
                .Include(b => b.Program)
                .FirstOrDefaultAsync(b => b.Id == id && b.EmployeeId == empId);

            if (benefit == null) return NotFound(new { message = "Không tìm thấy quyền lợi hoặc không có quyền truy cập." });

            return Json(new {
                id = benefit.Id,
                programName = benefit.Program?.Name,
                programDescription = benefit.Program?.Description,
                benefitType = benefit.Program?.BenefitType,
                effectiveFrom = benefit.EffectiveFrom,
                effectiveTo = benefit.EffectiveTo,
                status = benefit.Status,
                amount = benefit.Amount,
                notes = benefit.Notes
            });
        }

        // --- 8. API: CHI TIẾT CHƯƠNG TRÌNH PHÚC LỢI ---
        [HttpGet]
        public async Task<IActionResult> GetProgramDetail(int id)
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Forbid();
            var empId = emp.Id;

            var program = await _context.BenefitPrograms.FindAsync(id);
            if (program == null || program.Status == "Draft") return NotFound(new { message = "Chương trình không tồn tại." });

            var enrollment = await _context.BenefitEnrollments
                .FirstOrDefaultAsync(e => e.BenefitProgramId == id && e.EmployeeId == empId);

            var now = DateTime.Now;
            var isWindowOpen = program.RegistrationStart <= now && program.RegistrationEnd >= now;
            var isEligible = isWindowOpen && program.Status == "Open" && !program.IsAutoApplied;
            
            string enrollmentStatus = "NOT_REGISTERED";
            if (enrollment != null) enrollmentStatus = enrollment.ApprovalStatus;
            
            if (enrollmentStatus == "PENDING_APPROVAL" || enrollmentStatus == "APPROVED")
            {
                isEligible = false;
            }

            return Json(new {
                id = program.Id,
                name = program.Name,
                description = program.Description,
                benefitType = program.BenefitType,
                status = program.Status,
                registrationStart = program.RegistrationStart,
                registrationEnd = program.RegistrationEnd,
                isAutoApplied = program.IsAutoApplied,
                requiresApproval = program.RequiresApproval,
                capacity = program.Capacity,
                enrollmentStatus = enrollmentStatus,
                isEligible = isEligible,
                eligibilityMessage = isEligible ? "" : (!isWindowOpen ? "Ngoài thời gian đăng ký" : (enrollment != null ? "Đã đăng ký" : "Không đủ điều kiện"))
            });
        }

        // --- 9. API: CHI TIẾT ĐĂNG KÝ (ENROLLMENT) ---
        [HttpGet]
        public async Task<IActionResult> GetEnrollmentDetail(int id)
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Forbid();
            var empId = emp.Id;

            var enrollment = await _context.BenefitEnrollments
                .Include(e => e.Program)
                .FirstOrDefaultAsync(e => e.Id == id && e.EmployeeId == empId);

            if (enrollment == null) return NotFound(new { message = "Không tìm thấy lượt đăng ký hoặc không có quyền truy cập." });

            return Json(new {
                id = enrollment.Id,
                programName = enrollment.Program?.Name,
                programDescription = enrollment.Program?.Description,
                registrationDate = enrollment.RegistrationDate,
                status = enrollment.ApprovalStatus,
                notes = enrollment.Notes,
                canWithdraw = enrollment.ApprovalStatus == "PENDING_APPROVAL"
            });
        }

        // --- 10. API: HỦY ĐĂNG KÝ (WITHDRAW) ---
        [HttpPost]
        public async Task<IActionResult> WithdrawEnrollment(int id)
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Forbid();
            var empId = emp.Id;

            var enrollment = await _context.BenefitEnrollments
                .FirstOrDefaultAsync(e => e.Id == id && e.EmployeeId == empId);

            if (enrollment == null) return NotFound(new { message = "Không tìm thấy lượt đăng ký." });
            
            if (enrollment.ApprovalStatus != "PENDING_APPROVAL")
            {
                return BadRequest(new { message = "Chỉ có thể hủy đăng ký khi yêu cầu đang chờ duyệt." });
            }

            enrollment.ApprovalStatus = "CANCELLED";
            await _context.SaveChangesAsync();
            await _auditLog.LogAsync("WithdrawEnrollment", "Benefit", "BenefitCore", id.ToString(), "Withdraw", "Success");

            return Ok(new { success = true, message = "Đã hủy đăng ký thành công." });
        }

        // --- 11. HR: PHÊ DUYỆT ĐĂNG KÝ ---
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> ApproveEnrollment(int id)
        {
            if (GetCurrentRole() != "HR") return Unauthorized();

            var enrollment = await _context.BenefitEnrollments.FindAsync(id);
            if (enrollment == null) return NotFound();

            var result = await _approvalEngine.ProcessDecisionAsync(enrollment, "Approve", GetCurrentUsername(), GetCurrentRole(), null, _policy);

            if (result.Success)
            {
                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            return BadRequest(new { message = result.Message });
        }

        // --- 12. HR: TỪ CHỐI ĐĂNG KÝ ---
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> RejectEnrollment(int id, string? reason)
        {
            if (GetCurrentRole() != "HR") return Unauthorized();

            var enrollment = await _context.BenefitEnrollments.FindAsync(id);
            if (enrollment == null) return NotFound();

            var result = await _approvalEngine.ProcessDecisionAsync(enrollment, "Reject", GetCurrentUsername(), GetCurrentRole(), reason, _policy);

            if (result.Success)
            {
                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            return BadRequest(new { message = result.Message });
        }

        // --- 13. HR: DANH SÁCH ĐĂNG KÝ CHỜ DUYỆT ---
        [HttpGet]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> GetPendingEnrollments()
        {
            if (GetCurrentRole() != "HR") return Unauthorized();

            var pending = await _context.BenefitEnrollments
                .Where(e => e.ApprovalStatus == "PENDING_APPROVAL")
                .Include(e => e.Program)
                .Include(e => e.Employee)
                .OrderBy(e => e.RegistrationDate)
                .Select(e => new {
                    e.Id,
                    employeeName = e.Employee != null ? e.Employee.Name : e.EmployeeNameSnapshot,
                    programName = e.Program != null ? e.Program.Name : "",
                    e.RegistrationDate,
                    e.Notes
                })
                .ToListAsync();

            return Json(pending);
        }
    }
}

