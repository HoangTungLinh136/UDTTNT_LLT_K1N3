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
using System.Collections.Generic;
using System.Security.Claims;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    public class TrainingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IApprovalEngine _approvalEngine;
        private readonly IWorkflowPolicy<CourseEnrollment> _policy;
        private readonly IAuditLogService _auditLog;
        private readonly IProtectedFileService _fileService;

        public TrainingController(
            ApplicationDbContext context,
            IApprovalEngine approvalEngine,
            IWorkflowPolicy<CourseEnrollment> policy,
            IAuditLogService auditLog,
            IProtectedFileService fileService)
        {
            _context = context;
            _approvalEngine = approvalEngine;
            _policy = policy;
            _auditLog = auditLog;
            _fileService = fileService;
        }

        private bool CheckLogin()
        {
            var session = HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.ISessionFeature>()?.Session;
            var user = session?.GetString("User");
            // Cookie authentication is the authoritative login state.  The
            // legacy session value is kept as a compatibility fallback for
            // older sign-in flows, but an authenticated HRMS user must not be
            // rejected simply because a session was not hydrated yet.
            return User?.Identity?.IsAuthenticated == true || !string.IsNullOrEmpty(user);
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

        public async Task<IActionResult> Index()
        {
            if (!CheckLogin()) return RedirectToAction("Login", "NhanVien");
            
            return View();
        }

        [HttpGet]
        [Authorize(Policy = AppPolicies.HROnly)]
        public IActionResult HrIndex() => View("HrIndex");

        [HttpGet]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> GetPendingEnrollments()
        {
            if (GetCurrentRole() != "HR") return Unauthorized();
            var pending = await _context.CourseEnrollments
                .Where(e => e.ProgressStatus == "PENDING_APPROVAL")
                .Include(e => e.Course).Include(e => e.Employee)
                .OrderBy(e => e.RegistrationDate)
                .Select(e => new { e.Id, employee = e.Employee != null ? e.Employee.Name : "", course = e.Course.Name, e.RegistrationDate, e.ProgressStatus })
                .ToListAsync();
            return Json(pending);
        }

        [HttpGet]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> GetHrEnrollments(string? search, string? status)
        {
            var q = _context.CourseEnrollments.AsNoTracking().Include(e => e.Course).Include(e => e.Employee).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(e =>
                    (e.Employee != null && e.Employee.EmployeeCode != null && e.Employee.EmployeeCode.Contains(s)) ||
                    (e.EmployeeNameSnapshot != null && e.EmployeeNameSnapshot.Contains(s)) ||
                    (e.Course.Code != null && e.Course.Code.Contains(s)) ||
                    (e.Course.Name != null && e.Course.Name.Contains(s)));
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                q = status == "PendingApproval"
                    ? q.Where(e => e.ApprovalStatus == "PENDING_APPROVAL")
                    : q.Where(e => e.ProgressStatus == status);
            }
            return Json(await q.OrderByDescending(e => e.RegistrationDate).Take(200)
                .Select(e => new
                {
                    e.Id,
                    employeeCode = e.Employee != null ? e.Employee.EmployeeCode : null,
                    employee = e.Employee != null ? e.Employee.Name : e.EmployeeNameSnapshot,
                    courseCode = e.Course.Code,
                    course = e.Course.Name,
                    e.RegistrationDate,
                    status = e.ApprovalStatus == "PENDING_APPROVAL" ? "PendingApproval" : e.ProgressStatus,
                    e.ProgressPercent,
                    certificate = _context.TrainingCertificates.Any(c => c.EnrollmentId == e.Id)
                }).ToListAsync());
        }

        [HttpGet]
        public async Task<IActionResult> GetMySummary()
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Forbid();
            var empId = emp.Id;

            var enrollments = await _context.CourseEnrollments
                .Include(e => e.Course)
                .Where(e => e.EmployeeId == empId)
                .ToListAsync();

            var inProgressCount = enrollments.Count(e => e.ProgressStatus == "InProgress");
            var completedCount = enrollments.Count(e => e.ProgressStatus == "Completed");
            var certificateCount = await _context.TrainingCertificates
                .CountAsync(c => c.EmployeeId == empId);
                
            var totalHours = enrollments
                .Where(e => e.ProgressStatus == "Completed" && e.Course != null && e.Course.DurationHours.HasValue)
                .Sum(e => e.Course.DurationHours.Value);

            return Json(new {
                inProgressCount,
                completedCount,
                certificateCount,
                totalTrainingHours = totalHours
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetActiveCourse()
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Forbid();
            var empId = emp.Id;

            var activeCourse = await _context.CourseEnrollments
                .Include(e => e.Course)
                .Where(e => e.EmployeeId == empId && e.ProgressStatus == "InProgress")
                .OrderBy(e => e.Course.EndDate) // Closest deadline first
                .Select(e => new {
                    e.Id,
                    e.CourseId,
                    CourseName = e.Course.Name,
                    e.Course.Instructor,
                    e.Course.Mode,
                    e.Course.StartDate,
                    e.Course.EndDate,
                    e.ProgressPercent,
                    Status = e.ProgressStatus
                })
                .FirstOrDefaultAsync();

            return Json(activeCourse);
        }

        [HttpGet]
        public async Task<IActionResult> GetMyCourses(string? search = null, string? status = null, int? page = null, int pageSize = 10)
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Forbid();
            var empId = emp.Id;

            if (page == null && search == null && status == null)
            {






            var legacyCourses = await _context.CourseEnrollments
                .Include(e => e.Course)
                .Where(e => e.EmployeeId == empId)
                .OrderByDescending(e => e.RegistrationDate)
                .Select(e => new {
                    e.Id,
                    e.CourseId,
                    CourseName = e.Course.Name,
                    e.Course.DurationHours,
                    e.Course.Mode,
                    e.ProgressPercent,
                    e.Result,
                    Status = e.ProgressStatus,
                    IsMandatory = false,
                    HasCertificate = _context.TrainingCertificates.Any(c => c.EnrollmentId == e.Id)
                })
                .ToListAsync();

            return Json(legacyCourses);
        
            }
            page = Math.Max(1, page ?? 1);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var query = _context.CourseEnrollments
                .AsNoTracking()
                .Where(e => e.EmployeeId == empId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(e =>
                    (e.Course.Name != null && e.Course.Name.Contains(term)) ||
                    (e.Course.Code != null && e.Course.Code.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "All")
            {
                query = query.Where(e => e.ProgressStatus == status ||
                    (status == "PendingApproval" && e.ApprovalStatus == "PENDING_APPROVAL"));
            }

            var totalCount = await query.CountAsync();
            var courses = await query
                .OrderByDescending(e => e.RegistrationDate)
                .Skip((page.Value - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new {
                    e.Id,
                    e.CourseId,
                    CourseName = e.Course.Name,
                    e.Course.DurationHours,
                    e.Course.Mode,
                    e.ProgressPercent,
                    e.Result,
                    Status = e.ProgressStatus,
                    e.Course.IsMandatory,
                    HasCertificate = _context.TrainingCertificates.Any(c => c.EnrollmentId == e.Id)
                })
                .ToListAsync();

            return Json(new
            {
                items = courses,
                totalCount,
                pageNumber = page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetCourseDetail(int id)
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Forbid();
            var empId = emp.Id;

            var enrollment = await _context.CourseEnrollments
                .Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.Id == id && e.EmployeeId == empId);

            if (enrollment == null) return NotFound("Không tìm thấy thông tin khóa học.");

            var materials = await _context.TrainingMaterials
                .Where(m => m.CourseId == enrollment.CourseId)
                .Select(m => new { m.Id, m.Title, m.Type, m.FileUrl })
                .ToListAsync();

            return Json(new {
                enrollment.Id,
                CourseName = enrollment.Course.Name,
                enrollment.Course.Code,
                enrollment.Course.Description,
                enrollment.Course.Category,
                enrollment.Course.Instructor,
                enrollment.Course.Mode,
                enrollment.Course.StartDate,
                enrollment.Course.EndDate,
                enrollment.Course.DurationHours,
                enrollment.Course.IsMandatory,
                enrollment.ProgressPercent,
                enrollment.Result,
                Status = enrollment.ProgressStatus,
                Materials = materials
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetRecommendedCourses()
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Forbid();
            var empId = emp.Id;

            var enrolledCourseIds = await _context.CourseEnrollments
                .Where(e => e.EmployeeId == empId && e.ProgressStatus != "Cancelled")
                .Select(e => e.CourseId)
                .ToListAsync();

            var now = DateTime.Now;

            var recommendations = await _context.TrainingCourses
                .Where(c => c.Status == "Open" && !enrolledCourseIds.Contains(c.Id))
                .Where(c => (!c.RegistrationStart.HasValue || c.RegistrationStart <= now) && (!c.RegistrationEnd.HasValue || c.RegistrationEnd >= now))
                .Select(c => new {
                    c.Id,
                    c.Name,
                    c.Category,
                    c.Mode,
                    c.DurationHours,
                    c.Capacity,
                    EnrolledCount = _context.CourseEnrollments.Count(e => e.CourseId == c.Id && e.ProgressStatus != "Cancelled"),
                    IsMandatory = false
                })
                .ToListAsync();

            return Json(recommendations);
        }

        [HttpPost]
        public async Task<IActionResult> RegisterCourse([FromBody] int courseId)
        {
            if (!CheckLogin()) return Unauthorized();

            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Forbid();

            var empId = emp.Id;

            var course = await _context.TrainingCourses
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null || course.Status != "Open")
                return BadRequest("Khóa học không hợp lệ hoặc không mở đăng ký.");

            var now = DateTime.Now;

            // Registration window
            if (course.RegistrationStart.HasValue &&
                course.RegistrationStart.Value > now)
            {
                return BadRequest("Chưa đến thời gian đăng ký.");
            }

            if (course.RegistrationEnd.HasValue &&
                course.RegistrationEnd.Value < now)
            {
                return BadRequest("Đã hết thời hạn đăng ký.");
            }

            // Department eligibility
            if (!string.IsNullOrWhiteSpace(course.EligibleDepartment) &&
                !string.Equals(
                    course.EligibleDepartment.Trim(),
                    emp.Department?.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(
                    "Bạn không thuộc phòng ban đủ điều kiện đăng ký khóa học này.");
            }

            // Position eligibility
            if (!string.IsNullOrWhiteSpace(course.EligiblePosition) &&
                !string.Equals(
                    course.EligiblePosition.Trim(),
                    emp.Position?.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(
                    "Bạn không thuộc vị trí đủ điều kiện đăng ký khóa học này.");
            }

            // Prerequisite
            if (course.PrerequisiteCourseId.HasValue)
            {
                var prerequisiteCompleted =
                    await _context.CourseEnrollments.AnyAsync(e =>
                        e.EmployeeId == empId &&
                        e.CourseId == course.PrerequisiteCourseId.Value &&
                        e.ProgressStatus == "Completed");

                if (!prerequisiteCompleted)
                {
                    return BadRequest(
                        "Bạn chưa hoàn thành khóa học tiên quyết.");
                }
            }

            // Duplicate + capacity must be protected together.
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            try
            {
                var existing =
                    await _context.CourseEnrollments.AnyAsync(e =>
                        e.CourseId == courseId &&
                        e.EmployeeId == empId &&
                        e.ProgressStatus != "Cancelled");

                if (existing)
                {
                    await transaction.RollbackAsync();

                    return BadRequest(
                        "Bạn đã đăng ký khóa học này rồi.");
                }

                if (course.Capacity.HasValue)
                {
                    var enrolledCount =
                        await _context.CourseEnrollments.CountAsync(e =>
                            e.CourseId == courseId &&
                            e.ProgressStatus != "Cancelled");

                    if (enrolledCount >= course.Capacity.Value)
                    {
                        await transaction.RollbackAsync();

                        return BadRequest(
                            "Khóa học đã đủ số lượng học viên.");
                    }
                }

                var newEnrollment = new CourseEnrollment
                {
                    CourseId = courseId,
                    EmployeeId = empId,
                    EmployeeNameSnapshot = emp.Name,
                    RegistrationDate = DateTime.Now,
                    ProgressStatus = course.RequiresApproval
                        ? "PENDING_APPROVAL"
                        : "Enrolled",
                    ProgressPercent = 0,
                    Result = "None",
                    Notes = ""
                };

                _context.CourseEnrollments.Add(newEnrollment);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _auditLog.LogAsync(
                    "RegisterCourse",
                    "Training",
                    "TrainingCore",
                    newEnrollment.Id.ToString(),
                    "Enrollment",
                    "Success");

                return Json(new
                {
                    success = true,
                    message = "Đăng ký khóa học thành công!"
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCertificateDetail(int enrollmentId)
        {
            if (!CheckLogin()) return Unauthorized();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Forbid();
            var empId = emp.Id;

            var cert = await _context.TrainingCertificates
                .Include(c => c.Enrollment)
                .ThenInclude(e => e.Course)
                .FirstOrDefaultAsync(c => c.EnrollmentId == enrollmentId && c.EmployeeId == empId);

            if (cert == null) return NotFound("Không tìm thấy chứng chỉ.");

            return Json(new {
                cert.CertificateNumber,
                cert.IssuedDate,
                CourseName = cert.Enrollment.Course.Name,
                EmployeeName = cert.EmployeeNameSnapshot,
                cert.Id
            });
        }

        // --- Manager Nomination endpoints ---
        [HttpPost]
        [Authorize(Roles = $"{AppRoles.Manager},{AppRoles.HR}")]
        public async Task<IActionResult> Nominate(int courseId, int employeeId)
        {
            if (GetCurrentRole() != "Manager" && GetCurrentRole() != "HR") return Unauthorized();

            var course = await _context.TrainingCourses.FindAsync(courseId);
            if (course == null) return NotFound("Course not found");

            var employee = await _context.Employees.FindAsync(employeeId);
            if (employee == null) return NotFound("Employee not found");

            if (GetCurrentRole() == "Manager")
            {
                var managerScope = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.IManagerScopeService>();
                if (!managerScope.CanAccessDepartment(employee.Department)) return Forbid();
            }

            var existing = await _context.CourseEnrollments.FirstOrDefaultAsync(e => e.CourseId == courseId && e.EmployeeId == employeeId);
            if (existing != null) return BadRequest("Already enrolled or nominated.");

            var enrollment = new CourseEnrollment
            {
                CourseId = courseId,
                EmployeeId = employeeId,
                EmployeeNameSnapshot = employee.Name,
                RegistrationDate = DateTime.Now,
                ProgressStatus = "PENDING_APPROVAL",
                ProgressPercent = 0,
                Result = "None",
                Notes = $"Nominated by {GetCurrentUsername()}"
            };
            
            _context.CourseEnrollments.Add(enrollment);
            await _context.SaveChangesAsync();
            
            await _auditLog.LogAsync("NominateTraining", "Training", "TrainingCore", enrollment.Id.ToString(), "Nomination", "Success");
            return Ok(new { success = true });
        }

        // --- HR Workflow endpoints ---
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> ApproveEnrollment(int id)
        {
            if (GetCurrentRole() != "HR") return Unauthorized();

            var enrollment = await _context.CourseEnrollments.FindAsync(id);
            if (enrollment == null) return NotFound();

            var result = await _approvalEngine.ProcessDecisionAsync(enrollment, "Approve", GetCurrentUsername(), GetCurrentRole(), null, _policy);
            
            if (result.Success)
            {
                await _context.SaveChangesAsync();
                await _auditLog.LogAsync("ApproveEnrollment", "Training", "TrainingCore", id.ToString(), "Approve", "Success");
                return Ok(new { success = true });
            }
            return BadRequest(result.Message);
        }

        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> RejectEnrollment(int id, string reason)
        {
            if (GetCurrentRole() != "HR") return Unauthorized();

            var enrollment = await _context.CourseEnrollments.FindAsync(id);
            if (enrollment == null) return NotFound();

            var result = await _approvalEngine.ProcessDecisionAsync(enrollment, "Reject", GetCurrentUsername(), GetCurrentRole(), reason, _policy);
            
            if (result.Success)
            {
                await _context.SaveChangesAsync();
                await _auditLog.LogAsync("RejectEnrollment", "Training", "TrainingCore", id.ToString(), "Reject", "Success");
                return Ok(new { success = true });
            }
            return BadRequest(result.Message);
        }

        [HttpPost]
        [Authorize(Roles = $"{AppRoles.Manager},{AppRoles.HR}")]
        public async Task<IActionResult> CompleteCourse(int enrollmentId, string result) 
        {
            if (GetCurrentRole() != "HR" && GetCurrentRole() != "Manager") return Unauthorized();

            var enrollment = await _context.CourseEnrollments.Include(e => e.Employee).Include(e => e.Course).FirstOrDefaultAsync(e => e.Id == enrollmentId);
            if (enrollment == null) return NotFound();

            if (enrollment.ProgressStatus != "Enrolled" &&
                enrollment.ProgressStatus != "InProgress")
            {
                return Conflict(new
                {
                    success = false,
                    message = $"Không thể hoàn thành khóa học từ trạng thái '{enrollment.ProgressStatus}'."
                });
            }

            if (GetCurrentRole() == "Manager")
            {
                var managerScope = HttpContext.RequestServices.GetRequiredService<giao_dien_demo.Services.IManagerScopeService>();
                if (!managerScope.CanAccessDepartment(enrollment.Employee?.Department)) return Forbid();
            }

            enrollment.ProgressStatus = "Completed";
            enrollment.Result = result;
            enrollment.CompletionDate = DateTime.Now;
            enrollment.ProgressPercent = 100;

            await _context.SaveChangesAsync();
            await _auditLog.LogAsync("CompleteCourse", "Training", "TrainingCore", enrollmentId.ToString(), "Complete", "Success");

            return Ok(new { success = true });
        }

        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> IssueCertificate(int enrollmentId, IFormFile certificateFile)
        {
            if (GetCurrentRole() != "HR") return Unauthorized();

            var enrollment = await _context.CourseEnrollments.Include(e => e.Employee).FirstOrDefaultAsync(e => e.Id == enrollmentId);
            if (enrollment == null) return NotFound();

            if (enrollment.ProgressStatus != "Completed")
                return BadRequest("Chỉ có thể cấp chứng chỉ cho khóa học đã hoàn thành.");
            if (await _context.TrainingCertificates.AnyAsync(c => c.EnrollmentId == enrollmentId))
                return BadRequest("Đăng ký này đã được cấp chứng chỉ.");

            if (certificateFile != null)
            {
                var protectedFile = await _fileService.SaveProtectedFileAsync("Certificates", enrollment.EmployeeId.ToString(), certificateFile);
                
                var cert = new TrainingCertificate
                {
                    EnrollmentId = enrollmentId,
                    EmployeeId = enrollment.EmployeeId,
                    EmployeeNameSnapshot = enrollment.Employee.Name,
                    IssuedDate = DateTime.Now,
                    CertificateNumber = "CERT-" + DateTime.Now.Ticks.ToString().Substring(0, 8),
                    CanonicalFileId = protectedFile.CanonicalId
                };

                _context.TrainingCertificates.Add(cert);
                await _context.SaveChangesAsync();
                await _auditLog.LogAsync("IssueCertificate", "Training", "TrainingCore", cert.Id.ToString(), "Certificate", "Success");
                return Ok(new { success = true });
            }

            return BadRequest("File missing");
        }
        
        [HttpGet]
        public async Task<IActionResult> DownloadCertificate(int certId)
        {
            if (!CheckLogin()) return Unauthorized();
            
            var cert = await _context.TrainingCertificates.FindAsync(certId);
            if (cert == null) return NotFound();

            // Check permissions: either HR, or the employee themselves
            var emp = await GetCurrentEmployeeAsync();
            if (GetCurrentRole() != "HR" && (emp == null || cert.EmployeeId != emp.Id))
            {
                return Forbid();
            }

            if(string.IsNullOrEmpty(cert.CanonicalFileId)) return NotFound();

            var file = await _fileService.GetProtectedFileAsync(cert.CanonicalFileId);
            return PhysicalFile(file.PhysicalPath, file.ContentType, file.OriginalName);
        }

        private async Task SeedTrainingData()
        {
            var course1 = new TrainingCourse { Name = "Đào tạo An toàn thông tin", Code = "SEC101", Description = "Khóa học bắt buộc về an toàn thông tin nội bộ.", Category = "Security", Mode = "E_Learning", Instructor = "IT Dept", IsMandatory = true, Status = "Open", DurationHours = 4, RequiresApproval = false, StartDate = DateTime.Now.AddDays(-10), EndDate = DateTime.Now.AddDays(20) };
            var course2 = new TrainingCourse { Name = "Kỹ năng Lãnh đạo", Code = "LDR202", Description = "Kỹ năng quản lý và lãnh đạo nhóm.", Category = "Soft Skills", Mode = "Online", Instructor = "Nguyễn Văn A", IsMandatory = false, Status = "Open", DurationHours = 12, Capacity = 20, RequiresApproval = true, RegistrationStart = DateTime.Now.AddDays(-5), RegistrationEnd = DateTime.Now.AddDays(5) };
            var course3 = new TrainingCourse { Name = "Clean Code với C#", Code = "DEV303", Description = "Viết code C# sạch và dễ bảo trì.", Category = "Technical", Mode = "Offline", Instructor = "Trần Thị B", IsMandatory = false, Status = "Open", DurationHours = 16, Capacity = 30, RequiresApproval = false };

            _context.TrainingCourses.AddRange(course1, course2, course3);
            await _context.SaveChangesAsync();

            var empId = GetCurrentEmployeeId();
            if (empId > 0)
            {
                var emp = await _context.Employees.FindAsync(empId);
                var empName = emp != null ? emp.Name : "Unknown";

                var enr1 = new CourseEnrollment { CourseId = course1.Id, EmployeeId = empId, EmployeeNameSnapshot = empName, RegistrationDate = DateTime.Now.AddDays(-10), ProgressStatus = "InProgress", ProgressPercent = 50, Result = "None", Notes = "" };
                var enr3 = new CourseEnrollment { CourseId = course3.Id, EmployeeId = empId, EmployeeNameSnapshot = empName, RegistrationDate = DateTime.Now.AddDays(-30), ProgressStatus = "Completed", ProgressPercent = 100, Result = "Pass", CompletionDate = DateTime.Now.AddDays(-2), Notes = "" };
                
                _context.CourseEnrollments.AddRange(enr1, enr3);
                await _context.SaveChangesAsync();

                var cert = new TrainingCertificate { EnrollmentId = enr3.Id, EmployeeId = empId, EmployeeNameSnapshot = empName, IssuedDate = DateTime.Now.AddDays(-1), CertificateNumber = "CERT-" + DateTime.Now.Ticks.ToString().Substring(0, 8) };
                _context.TrainingCertificates.Add(cert);

                var mat1 = new TrainingMaterial { CourseId = course1.Id, Title = "Slide Bài 1", Type = "PDF", FileUrl = "#" };
                _context.TrainingMaterials.Add(mat1);

                await _context.SaveChangesAsync();
            }
        }
    }
}



