using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Models;
using giao_dien_demo.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using giao_dien_demo.Hubs;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<DashboardHub> _hubContext;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly giao_dien_demo.Services.IManagerAssignmentService _managerAssignmentService;
        private readonly giao_dien_demo.Services.IEmployeeCodeAssignmentService _employeeCodeAssignmentService;
        private readonly giao_dien_demo.Services.IDirectManagerService _directManagerService;
        private readonly giao_dien_demo.Services.IOrganizationHierarchyService _hierarchyService;

        // ✅ KHAI BÁO BIẾN TĨNH: Đảm bảo các Controller khác không bị lỗi "Red line"
        public static List<Employee> list = new List<Employee>();
        private readonly giao_dien_demo.Services.IAuditLogService _auditLogService;

        public EmployeeController(ApplicationDbContext context,
                                  IHubContext<DashboardHub> hubContext,
                                  IWebHostEnvironment hostEnvironment,
                                  giao_dien_demo.Services.IAuditLogService auditLogService,
                                  giao_dien_demo.Services.IManagerAssignmentService managerAssignmentService,
                                  giao_dien_demo.Services.IEmployeeCodeAssignmentService? employeeCodeAssignmentService = null,
                                  giao_dien_demo.Services.IDirectManagerService? directManagerService = null,
                                  giao_dien_demo.Services.IOrganizationHierarchyService? hierarchyService = null)
        {
            _context = context;
            _hubContext = hubContext;
            _hostEnvironment = hostEnvironment;
            _auditLogService = auditLogService;
            _managerAssignmentService = managerAssignmentService;
            _employeeCodeAssignmentService = employeeCodeAssignmentService ?? new giao_dien_demo.Services.EmployeeCodeAssignmentService(context);
            _directManagerService = directManagerService ?? new giao_dien_demo.Services.DirectManagerService(context, auditLogService);
            _hierarchyService = hierarchyService ?? new giao_dien_demo.Services.OrganizationHierarchyService(
                context,
                new giao_dien_demo.Services.ManagerScopeService(context, new Microsoft.AspNetCore.Http.HttpContextAccessor()),
                new giao_dien_demo.Services.LeadershipScopeService(context, new Microsoft.AspNetCore.Http.HttpContextAccessor()));
        }

        // =========================================================================
        // 🔥 1. DÀNH SÁCH NHÂN SỰ - XỬ LÝ TRÙNG MÃ & TÌM KIẾM
        // =========================================================================
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> Index(string? searchName, string? department, string? status = "Active", int? page = null, string? view = null)
        {
            ViewBag.ActiveView = view;
            // --- TRUY VẤN TÌM KIẾM VÀ LỌC ---
            var query = _context.Employees
                .Include(e => e.Manager)
                .Include(e => e.DepartmentEntity)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchName))
            {
                query = query.Where(e => (e.Name != null && e.Name.Contains(searchName)) ||
                                         (e.EmployeeCode != null && e.EmployeeCode.Contains(searchName)));
            }

            if (!string.IsNullOrEmpty(department))
                query = query.Where(e => e.Department == department);
                
            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                query = query.Where(e => e.Status == status);
            }
            else if (string.IsNullOrEmpty(status))
            {
                query = query.Where(e => e.Status != "Offboarded" && e.Status != "Terminated");
            }

            ViewBag.Departments = await _context.Employees
                                        .Where(e => e.Department != null)
                                        .Select(e => e.Department)
                                        .Distinct()
                                        .ToListAsync();

            ViewBag.CurrentSearch = searchName;
            ViewBag.CurrentDept = department;

            if (page.HasValue)
            {
                ViewBag.TotalItems = await query.CountAsync();
                ViewBag.PageNumber = Math.Max(1, page.Value);
                ViewBag.PageSize = 10;
                return View(await query.OrderBy(e => e.EmployeeCode).Skip((Math.Max(1, page.Value) - 1) * 10).Take(10).ToListAsync());
            }
            return View(await query.OrderBy(e => e.EmployeeCode).ToListAsync());
        }

        // --- 2. XEM CHI TIẾT HỒ SƠ ---
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> Details(int id)
        {
            var emp = await _context.Employees
                .Include(e => e.Manager)
                .Include(e => e.DepartmentEntity)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (emp == null) return NotFound();

            // Link related records
            ViewBag.Contracts = await _context.Contracts.Where(c => c.EmployeeId == emp.Id).ToListAsync();
            ViewBag.Appointments = await _context.AppointmentRecords.Where(a => a.EmployeeId == emp.Id).ToListAsync();
            ViewBag.Dismissals = await _context.DismissalRecords.Where(d => d.EmployeeId == emp.Id).ToListAsync();
            ViewBag.Transfers = await _context.TransferRecords.Where(t => t.EmployeeId == emp.Id).ToListAsync();
            ViewBag.Rewards = await _context.RewardRecords.Where(r => r.EmployeeId == emp.Id).ToListAsync();
            ViewBag.Leaves = await _context.Leaves.Where(l => l.EmployeeId == emp.Id).ToListAsync();

            // Direct Manager & Reporting History (OD-07, OD-08, OD-09, OD-11)
            ViewBag.ReportingHistories = await _context.EmployeeReportingHistories
                .Include(h => h.PreviousManager)
                .Include(h => h.NewManager)
                .Include(h => h.ChangedByUser)
                .Where(h => h.EmployeeId == emp.Id)
                .OrderByDescending(h => h.EffectiveDate)
                .ThenByDescending(h => h.Id)
                .ToListAsync();

            ViewBag.EligibleManagers = await _context.Employees
                .Where(e => e.Id != emp.Id && e.Status == "Active")
                .OrderBy(e => e.Name)
                .ToListAsync();

            if (_managerAssignmentService != null)
                ViewBag.ManagerScope = await _managerAssignmentService.GetDetailsAsync(emp.Id);
            
            return View(emp);
        }

        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateManagerAssignment(ManagerAssignmentInput input)
        {
            var result = await _managerAssignmentService.CreateAsync(
                input.EmployeeId, input.OrganizationalUnit, input.EffectiveFrom, input.EffectiveTo,
                CurrentActor.Username(User) ?? HttpContext.Session.GetString("User") ?? "HR");
            if (!result.Success) TempData["Error"] = result.Error;
            else TempData["Success"] = "Đã phân công phạm vi quản lý thành công.";
            return RedirectToAction(nameof(Details), new { id = input.EmployeeId });
        }

        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateManagerAssignment(ManagerAssignmentInput input)
        {
            if (!input.AssignmentId.HasValue) return BadRequest("Thiếu mã phân công.");
            var result = await _managerAssignmentService.UpdateAsync(
                input.AssignmentId.Value, input.OrganizationalUnit, input.EffectiveFrom, input.EffectiveTo,
                CurrentActor.Username(User) ?? HttpContext.Session.GetString("User") ?? "HR");
            if (!result.Success) TempData["Error"] = result.Error;
            else TempData["Success"] = "Đã cập nhật phạm vi quản lý.";
            return RedirectToAction(nameof(Details), new { id = input.EmployeeId });
        }

        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EndManagerAssignment(int employeeId, int assignmentId, DateTime? effectiveTo)
        {
            var result = await _managerAssignmentService.EndAsync(
                assignmentId, effectiveTo,
                CurrentActor.Username(User) ?? HttpContext.Session.GetString("User") ?? "HR");
            if (!result.Success) TempData["Error"] = result.Error;
            else TempData["Success"] = "Đã kết thúc phạm vi quản lý. Lịch sử được giữ nguyên.";
            return RedirectToAction(nameof(Details), new { id = employeeId });
        }

        // =========================================================================
        // WF-01: THAY ĐỔI QUẢN LÝ TRỰC TIẾP (OD-07, OD-08, OD-09, OD-11, BD-06)
        // =========================================================================
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeDirectManager(DirectManagerChangeInput input)
        {
            var currentActor = CurrentActor.Username(User) ?? HttpContext.Session.GetString("User") ?? "HR";
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == currentActor);

            var request = new giao_dien_demo.Services.DirectManagerChangeRequest
            {
                EmployeeId = input.EmployeeId,
                NewManagerId = input.NewManagerId,
                AllowNullManagerException = input.AllowNullManagerException,
                NullManagerReason = input.NullManagerReason,
                Reason = input.Reason,
                RowVersion = input.RowVersion
            };

            var result = await _directManagerService.ChangeDirectManagerAsync(request, currentActor, currentUser?.Id);
            if (!result.Success)
            {
                TempData["Error"] = result.Error;
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = result.Error });
                }
                return RedirectToAction(nameof(Details), new { id = input.EmployeeId });
            }

            TempData["Success"] = "Đã thay đổi quản lý trực tiếp thành công.";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, historyId = result.History?.Id });
            }
            return RedirectToAction(nameof(Details), new { id = input.EmployeeId });
        }

        // =========================================================================
        // 🔥 CÂY PHÂN CẤP QUẢN LÝ TRỰC TIẾP (OD-07 & OD-11 COMPLIANT)
        // =========================================================================
        [Authorize]
        public async Task<IActionResult> Hierarchy(string? department = null)
        {
            var model = await _hierarchyService.BuildHierarchyAsync(User, department);
            return View(model);
        }

        // --- 3. TRANG CÁ NHÂN: TỰ CẬP NHẬT & UPLOAD ẢNH ---
        [Authorize]
        public async Task<IActionResult> MyProfile()
        {
            var currentUser = await CurrentActor.ResolveUserAsync(User, _context);
            if (currentUser?.EmployeeId == null) return Forbid();
            var emp = await _context.Employees
                .Include(e => e.Manager)
                .Include(e => e.DepartmentEntity)
                .FirstOrDefaultAsync(e => e.Id == currentUser.EmployeeId.Value);
            if (emp == null) return NotFound("Hồ sơ không tồn tại.");
            return View(emp);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMyProfile(Employee emp, IFormFile? AvatarFile)
        {
            var currentUser = await CurrentActor.ResolveUserAsync(User, _context);
            if (currentUser?.EmployeeId == null) return Forbid();
            var existing = await _context.Employees.FindAsync(currentUser.EmployeeId.Value);
            if (existing != null)
            {
                var submittedEmail = emp.Email?.Trim();
                var submittedCitizenId = emp.CitizenId?.Trim();
                if (!string.IsNullOrWhiteSpace(submittedEmail) && !new EmailAddressAttribute().IsValid(submittedEmail))
                    ModelState.AddModelError(nameof(Employee.Email), "Email không hợp lệ.");
                if (!string.IsNullOrWhiteSpace(submittedCitizenId) && !Regex.IsMatch(submittedCitizenId, "^\\d{9,12}$"))
                    ModelState.AddModelError(nameof(Employee.CitizenId), "CCCD/CMND phải gồm 9 hoặc 12 chữ số.");

                // Validate the submitted self-service payload before applying
                // the explicit allow-list below.  Authoritative Employee
                // fields remain ignored even if a client posts them.
                if (!ModelState.IsValid)
                {
                    emp.Id = existing.Id;
                    emp.Name = existing.Name;
                    emp.EmployeeCode = existing.EmployeeCode;
                    emp.JobTitle = existing.JobTitle;
                    emp.AvatarPath = existing.AvatarPath;
                    return View(emp);
                }

                bool hasAvatarUpload = AvatarFile != null && (!string.IsNullOrEmpty(AvatarFile.FileName) || AvatarFile.Length > 0);
                if (hasAvatarUpload)
                {
                    if (!ValidateAvatarFile(AvatarFile!, out var validationError, out var extension))
                    {
                        ModelState.AddModelError("AvatarFile", validationError);
                        return View(existing);
                    }

                    string fileName = Guid.NewGuid().ToString() + extension;
                    string path = Path.Combine(_hostEnvironment.WebRootPath, "uploads", "avatars");
                    if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                    if (!string.IsNullOrEmpty(existing.AvatarPath))
                    {
                        var oldPath = Path.Combine(_hostEnvironment.WebRootPath, existing.AvatarPath.TrimStart('/'));
                        if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                    }

                    using (var fileStream = new FileStream(Path.Combine(path, fileName), FileMode.Create))
                    {
                        await AvatarFile!.CopyToAsync(fileStream);
                    }
                    existing.AvatarPath = "/uploads/avatars/" + fileName;
                }

                // A null value means the field was not part of a partial
                // self-service payload; preserve the stored value in that case.
                existing.Email = submittedEmail ?? existing.Email;
                existing.PhoneNumber = emp.PhoneNumber?.Trim();
                existing.Address = emp.Address?.Trim();
                existing.CitizenId = submittedCitizenId ?? existing.CitizenId;
                existing.Gender = emp.Gender?.Trim();
                existing.BirthDate = emp.BirthDate;
                existing.BankName = emp.BankName;
                existing.BankAccountNumber = emp.BankAccountNumber;

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError(string.Empty, "Không thể lưu hồ sơ lúc này. Vui lòng thử lại.");
                    return View(emp);
                }

                await _auditLogService.LogAsync("Update", "Employee", "MyProfile", existing.Id.ToString(), "ProfileUpdated", "Success");
                return RedirectToAction(nameof(MyProfile));
            }
            return View(emp);
        }

        public static bool ValidateAvatarFile(IFormFile file, out string errorMessage, out string sanitizedExtension)
        {
            errorMessage = string.Empty;
            sanitizedExtension = string.Empty;

            if (file == null || file.Length == 0)
            {
                errorMessage = "File tải lên không có dữ liệu (0 byte).";
                return false;
            }

            if (file.Length > 5 * 1024 * 1024)
            {
                errorMessage = "Kích thước file ảnh vượt quá giới hạn cho phép (tối đa 5MB).";
                return false;
            }

            var rawExt = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(rawExt))
            {
                errorMessage = "Định dạng file không hợp lệ (thiếu phần mở rộng).";
                return false;
            }
            var ext = rawExt.ToLowerInvariant();

            bool isJpg = ext == ".jpg" || ext == ".jpeg";
            bool isPng = ext == ".png";

            if (!isJpg && !isPng)
            {
                errorMessage = "Chỉ chấp nhận định dạng ảnh .jpg, .jpeg hoặc .png.";
                return false;
            }

            var contentType = file.ContentType?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(contentType))
            {
                errorMessage = "Không xác định được Content-Type của file.";
                return false;
            }

            if (isJpg && contentType != "image/jpeg")
            {
                errorMessage = "Content-Type không khớp với định dạng JPEG (yêu cầu image/jpeg).";
                return false;
            }
            if (isPng && contentType != "image/png")
            {
                errorMessage = "Content-Type không khớp với định dạng PNG (yêu cầu image/png).";
                return false;
            }

            // Stream safety: read header bytes only, without truncating or mutating stream for subsequent operations
            byte[] header = new byte[8];
            int bytesRead;
            using (var stream = file.OpenReadStream())
            {
                bytesRead = stream.Read(header, 0, header.Length);
            }

            if (isJpg)
            {
                // JPEG magic bytes: FF D8 FF
                if (bytesRead < 3 || header[0] != 0xFF || header[1] != 0xD8 || header[2] != 0xFF)
                {
                    errorMessage = "Nội dung file không đúng định dạng JPEG (sai signature magic bytes).";
                    return false;
                }
            }
            else if (isPng)
            {
                // PNG magic bytes: 89 50 4E 47 0D 0A 1A 0A
                if (bytesRead < 8 ||
                    header[0] != 0x89 || header[1] != 0x50 || header[2] != 0x4E || header[3] != 0x47 ||
                    header[4] != 0x0D || header[5] != 0x0A || header[6] != 0x1A || header[7] != 0x0A)
                {
                    errorMessage = "Nội dung file không đúng định dạng PNG (sai signature magic bytes).";
                    return false;
                }
            }

            sanitizedExtension = ext;
            return true;
        }

        // --- 4. SỬA THÔNG TIN (HR) ---
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> Edit(int id)
        {
            var emp = await _context.Employees.FindAsync(id);
            if (emp == null) return NotFound();
            return View(emp);
        }

        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind("Id,Name,Department,Position,JobTitle,AcademicLevel,Salary")] Employee emp)
        {
            if (!ModelState.IsValid)
                return View(emp);

            var existing = await _context.Employees.FindAsync(emp.Id);
            if (existing == null)
                return NotFound();

            // Employee profile fields only. ManagerAssignment is managed by its own HROnly actions.
            existing.Name = emp.Name;
            existing.Department = emp.Department;
            existing.Position = emp.Position;
            existing.JobTitle = emp.JobTitle;
            existing.AcademicLevel = emp.AcademicLevel;
            existing.Salary = emp.Salary;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "Không thể lưu thông tin nhân viên. Vui lòng thử lại.");
                return View(emp);
            }

            await _auditLogService.LogAsync("Edit", "Employee", "EmployeeCore", emp.Id.ToString(), "EmployeeUpdated", "Success");
            await _hubContext.Clients.Group("role:HR").SendAsync("UserUpdated", new { fullName = emp.Name });
            TempData["Success"] = "Đã cập nhật thông tin nhân viên.";
            return RedirectToAction(nameof(Index));
        }

        // --- 5. XÓA ---
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            // Employee deletion must be routed through Offboarding workflow
            return BadRequest("Phải sử dụng quy trình Offboarding để cho thôi việc nhân viên.");
        }

        // --- 6. TẠO MỚI ---
        [HttpGet]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> Create()
        {
            await PopulateCreateReferenceDataAsync();
            return View(new EmployeeCreateViewModel());
        }

        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateCreateReferenceDataAsync();
                return View(model);
            }

            // EmployeeCode is deliberately absent from the ViewModel and is
            // always assigned by the canonical MGR-07E allocator.
            var emp = new Employee
            {
                Name = model.Name?.Trim(),
                Email = model.Email?.Trim(),
                PhoneNumber = model.PhoneNumber?.Trim(),
                Address = model.Address?.Trim(),
                Gender = model.Gender,
                BirthDate = model.BirthDate,
                CitizenId = model.CitizenId?.Trim(),
                Department = model.Department?.Trim(),
                Position = model.Position,
                JobTitle = model.JobTitle,
                AcademicLevel = model.AcademicLevel,
                Salary = model.Salary,
                Status = "Active",
                EmployeeCode = null
            };

            _context.Employees.Add(emp);
            await _context.SaveChangesAsync();
            string code;
            try
            {
                code = await _employeeCodeAssignmentService.EnsureAssignedAsync(emp);
            }
            catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException)
            {
                // The allocator may require a persisted identity. Remove the
                // just-created row if allocation fails so no partial employee
                // remains and no code is consumed by a failed request.
                _context.ChangeTracker.Clear();
                var persisted = await _context.Employees.FindAsync(emp.Id);
                if (persisted is not null)
                {
                    _context.Employees.Remove(persisted);
                    await _context.SaveChangesAsync();
                }

                ModelState.AddModelError(string.Empty, "Không thể cấp mã nhân viên. Vui lòng thử lại.");
                await PopulateCreateReferenceDataAsync();
                return View(model);
            }

            await _auditLogService.LogAsync(
                "Create", "Employee", "EmployeeCore", emp.Id.ToString(),
                "EmployeeCreated", "Success",
                newValues: $"EmployeeCode:{code}");

            if (_hubContext != null)
            {
                var count = await _context.Employees.CountAsync(e => e.Status == "Active" || e.Status == "Probation");
                await _hubContext.Clients.Group("role:HR").SendAsync("UpdateEmployeeCount", count);
            }
            return RedirectToAction(nameof(Details), new { id = emp.Id });
        }

        private async Task PopulateCreateReferenceDataAsync()
        {
            ViewBag.Departments = await _context.Employees
                .Where(e => e.Department != null && e.Department != "")
                .Select(e => e.Department!)
                .Distinct()
                .OrderBy(e => e)
                .ToListAsync();
        }
    }
}
