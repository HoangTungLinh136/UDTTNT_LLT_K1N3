using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Controllers
{
    [Authorize(Policy = AppPolicies.HROnly)]
    public class IncidentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.IProtectedFileService _fileService;

        public IncidentController(ApplicationDbContext context, giao_dien_demo.Services.IProtectedFileService fileService)
        {
            _context = context;
            _fileService = fileService;
        }

        public async Task<IActionResult> Index(string searchName = "", string incidentType = "", string severity = "", string status = "")
        {
            // ViewBags for dropdowns
            ViewBag.IncidentTypes = new List<string> { "Kỷ luật", "Tai nạn lao động", "Xung đột", "Vi phạm bảo mật", "Thất thoát tài sản", "Khác" };
            ViewBag.Severities = new List<string> { "LOW", "MEDIUM", "HIGH", "CRITICAL" };

            // Stats
            ViewBag.StatNew = await _context.IncidentRecords.CountAsync(r => r.Status == "NEW");
            ViewBag.StatInvestigating = await _context.IncidentRecords.CountAsync(r => r.Status == "INVESTIGATING");
            ViewBag.StatProcessing = await _context.IncidentRecords.CountAsync(r => r.Status == "PROCESSING");
            ViewBag.StatCritical = await _context.IncidentRecords.CountAsync(r => r.Severity == "CRITICAL");

            // Current filters
            ViewBag.CurrentSearch = searchName;
            ViewBag.CurrentIncidentType = incidentType;
            ViewBag.CurrentSeverity = severity;
            ViewBag.CurrentStatus = status;

            var query = _context.IncidentRecords
                .Include(r => r.Participants)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                var lowerSearch = searchName.ToLower();
                query = query.Where(r => 
                    (r.IncidentCode != null && r.IncidentCode.ToLower().Contains(lowerSearch)) ||
                    (r.Title != null && r.Title.ToLower().Contains(lowerSearch)) ||
                    r.Participants.Any(p => p.EmployeeCodeSnapshot != null && p.EmployeeCodeSnapshot.ToLower().Contains(lowerSearch)) ||
                    r.Participants.Any(p => p.EmployeeNameSnapshot != null && p.EmployeeNameSnapshot.ToLower().Contains(lowerSearch))
                );
            }

            if (!string.IsNullOrWhiteSpace(incidentType))
            {
                query = query.Where(r => r.IncidentType == incidentType);
            }

            if (!string.IsNullOrWhiteSpace(severity))
            {
                query = query.Where(r => r.Severity == severity);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.Status == status);
            }

            var result = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
            return View(result);
        }

        [HttpGet]
        public async Task<IActionResult> SearchEmployee(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return Json(new List<object>());
            var lowerQuery = query.ToLower();
            var emps = await _context.Employees
                .Where(e => (e.EmployeeCode != null && e.EmployeeCode.ToLower().Contains(lowerQuery)) 
                         || (e.Name != null && e.Name.ToLower().Contains(lowerQuery)))
                .Take(10)
                .Select(e => new { 
                    id = e.Id, 
                    employeeCode = e.EmployeeCode, 
                    name = e.Name, 
                    department = e.Department,
                    jobTitle = e.JobTitle,
                    position = e.Position
                }).ToListAsync();
            return Json(emps);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string title, string incidentType, string severity, DateTime occurredAt, string description, string location, int[] employeeIds)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(incidentType) || string.IsNullOrWhiteSpace(severity) || string.IsNullOrWhiteSpace(description))
            {
                return Json(new { success = false, message = "Vui lòng điền đầy đủ các trường bắt buộc." });
            }

            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            
            // Tự tạo Incident Code
            int countToday = await _context.IncidentRecords.CountAsync(x => x.CreatedAt.Date == DateTime.Now.Date);
            string incidentCode = $"INC-{DateTime.Now:yyyyMMdd}-{(countToday + 1).ToString("D3")}";

            var record = new IncidentRecord
            {
                IncidentCode = incidentCode,
                Title = title,
                IncidentType = incidentType,
                Severity = severity,
                OccurredAt = occurredAt,
                Description = description,
                Location = location,
                Status = "NEW",
                ReportedBy = currentUser,
                CreatedAt = DateTime.Now,
                CreatedBy = currentUser,
                UpdatedAt = DateTime.Now,
                UpdatedBy = currentUser
            };

            _context.IncidentRecords.Add(record);
            await _context.SaveChangesAsync(); // Cần Id để add Participants

            if (employeeIds != null && employeeIds.Length > 0)
            {
                foreach (var empId in employeeIds)
                {
                    var emp = await _context.Employees.FindAsync(empId);
                    if (emp != null)
                    {
                        var participant = new IncidentParticipant
                        {
                            IncidentRecordId = record.Id,
                            EmployeeId = emp.Id,
                            EmployeeCodeSnapshot = emp.EmployeeCode,
                            EmployeeNameSnapshot = emp.Name,
                            DepartmentSnapshot = emp.Department,
                            PositionSnapshot = emp.Position,
                            ParticipantRole = "RELATED_PERSON"
                        };
                        _context.IncidentParticipants.Add(participant);
                    }
                }
            }

            var history = new IncidentHistory
            {
                IncidentRecordId = record.Id,
                Timestamp = DateTime.Now,
                Actor = currentUser,
                Action = "CREATE_INCIDENT",
                Details = $"Đã tạo hồ sơ sự cố {record.IncidentCode}"
            };
            _context.IncidentHistories.Add(history);

            await _context.SaveChangesAsync();
            return Json(new { success = true, id = record.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var record = await _context.IncidentRecords
                .Include(r => r.Participants)
                .Include(r => r.Witnesses)
                .Include(r => r.Evidences)
                .Include(r => r.Histories)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (record == null) return Json(new { success = false });

            // Order histories
            var histories = record.Histories.OrderByDescending(h => h.Timestamp).ToList();

            return Json(new { 
                success = true, 
                data = new {
                    record.Id,
                    record.IncidentCode,
                    record.Title,
                    record.IncidentType,
                    record.Severity,
                    OccurredAt = record.OccurredAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    record.Location,
                    record.Description,
                    record.ReportedBy,
                    record.ResponsiblePersonNameSnapshot,
                    record.Status,
                    record.InvestigationSummary,
                    record.ProcessingNotes,
                    record.Conclusion,
                    record.Resolution,
                    CreatedAt = record.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    Participants = record.Participants.Select(p => new {
                        p.EmployeeCodeSnapshot,
                        p.EmployeeNameSnapshot,
                        p.DepartmentSnapshot,
                        p.PositionSnapshot
                    }).ToList(),
                    Witnesses = record.Witnesses.Select(w => new {
                        w.WitnessName,
                        w.Department,
                        w.Notes
                    }).ToList(),
                    Evidences = record.Evidences.Select(e => new {
                        e.Id,
                        e.FileName,
                        e.FilePath,
                        e.UploadedBy,
                        UploadedAt = e.UploadedAt.ToString("yyyy-MM-ddTHH:mm:ss")
                    }).ToList(),
                    Histories = histories.Select(h => new {
                        Timestamp = h.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss"),
                        h.Actor,
                        h.Action,
                        h.Details
                    }).ToList()
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartInvestigation(int id)
        {
            var record = await _context.IncidentRecords.FindAsync(id);
            if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            if (record.Status != "NEW") return Json(new { success = false, message = "Trạng thái không hợp lệ." });

            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            record.Status = "INVESTIGATING";
            record.InvestigationStartedAt = DateTime.Now;
            record.InvestigationStartedBy = currentUser;
            record.UpdatedAt = DateTime.Now;
            record.UpdatedBy = currentUser;

            var history = new IncidentHistory
            {
                IncidentRecordId = record.Id,
                Timestamp = DateTime.Now,
                Actor = currentUser,
                Action = "START_INVESTIGATION",
                Details = "Bắt đầu xác minh sự cố."
            };
            _context.IncidentHistories.Add(history);

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve(int id, string investigationSummary, string conclusion, string resolution)
        {
            var record = await _context.IncidentRecords.FindAsync(id);
            if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            if (record.Status != "INVESTIGATING" && record.Status != "PROCESSING") 
                return Json(new { success = false, message = "Trạng thái không hợp lệ." });

            if (string.IsNullOrWhiteSpace(conclusion))
            {
                return Json(new { success = false, message = "Vui lòng nhập Kết luận." });
            }

            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            
            record.Status = "RESOLVED";
            record.InvestigationSummary = investigationSummary;
            record.Conclusion = conclusion;
            record.Resolution = resolution;
            record.ResolvedAt = DateTime.Now;
            record.ResolvedBy = currentUser;
            record.UpdatedAt = DateTime.Now;
            record.UpdatedBy = currentUser;

            var history = new IncidentHistory
            {
                IncidentRecordId = record.Id,
                Timestamp = DateTime.Now,
                Actor = currentUser,
                Action = "RESOLVE_INCIDENT",
                Details = "Đã kết luận và xử lý sự cố."
            };
            _context.IncidentHistories.Add(history);

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(int id)
        {
            var record = await _context.IncidentRecords.FindAsync(id);
            if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            if (record.Status != "RESOLVED") 
                return Json(new { success = false, message = "Trạng thái không hợp lệ. Chỉ có thể đóng hồ sơ đã được kết luận (RESOLVED)." });

            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            
            record.Status = "CLOSED";
            record.ClosedAt = DateTime.Now;
            record.ClosedBy = currentUser;
            record.UpdatedAt = DateTime.Now;
            record.UpdatedBy = currentUser;

            var history = new IncidentHistory
            {
                IncidentRecordId = record.Id,
                Timestamp = DateTime.Now,
                Actor = currentUser,
                Action = "CLOSE_INCIDENT",
                Details = "Đóng hồ sơ sự cố."
            };
            _context.IncidentHistories.Add(history);

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string reason)
        {
            var record = await _context.IncidentRecords.FindAsync(id);
            if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            if (record.Status != "NEW" && record.Status != "INVESTIGATING" && record.Status != "PROCESSING")
                return Json(new { success = false, message = "Chỉ có thể hủy hồ sơ mới hoặc đang xác minh/xử lý." });

            if (string.IsNullOrWhiteSpace(reason))
                return Json(new { success = false, message = "Vui lòng nhập lý do hủy." });

            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            
            record.Status = "CANCELLED";
            record.CancelledAt = DateTime.Now;
            record.CancelledBy = currentUser;
            record.CancellationReason = reason;
            record.UpdatedAt = DateTime.Now;
            record.UpdatedBy = currentUser;

            var history = new IncidentHistory
            {
                IncidentRecordId = record.Id,
                Timestamp = DateTime.Now,
                Actor = currentUser,
                Action = "CANCEL_INCIDENT",
                Details = $"Hủy hồ sơ sự cố. Lý do: {reason}"
            };
            _context.IncidentHistories.Add(history);

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadEvidence(int id, IFormFile file, string description)
        {
            var record = await _context.IncidentRecords.FindAsync(id);
            if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            if (record.Status == "CLOSED" || record.Status == "CANCELLED")
                return Json(new { success = false, message = "Không thể đính kèm tài liệu vào hồ sơ đã đóng hoặc hủy." });

            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "File không hợp lệ." });

            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var (canonicalId, _) = await _fileService.SaveProtectedFileAsync("Incident", record.Id.ToString(), file);

            var evidence = new IncidentEvidence
            {
                IncidentRecordId = record.Id,
                FileName = file.FileName,
                FilePath = canonicalId,
                ContentType = file.ContentType,
                Description = description,
                UploadedBy = currentUser,
                UploadedAt = DateTime.Now
            };

            _context.IncidentEvidences.Add(evidence);

            var history = new IncidentHistory
            {
                IncidentRecordId = record.Id,
                Timestamp = DateTime.Now,
                Actor = currentUser,
                Action = "ADD_EVIDENCE",
                Details = $"Tải lên bằng chứng: {file.FileName}"
            };
            _context.IncidentHistories.Add(history);

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> DownloadEvidence(int evidenceId)
        {
            var evidence = await _context.IncidentEvidences.FindAsync(evidenceId);
            if (evidence == null) return NotFound("Evidence not found.");

            var record = await _context.IncidentRecords.FindAsync(evidence.IncidentRecordId);
            if (record == null) return NotFound();

            try
            {
                var (physicalPath, contentType, originalName) = await _fileService.GetProtectedFileAsync(evidence.FilePath);
                return PhysicalFile(physicalPath, evidence.ContentType ?? contentType, evidence.FileName ?? originalName);
            }
            catch (Exception)
            {
                return NotFound("File is missing or access denied.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddWitness(int id, string witnessName, string department, string notes)
        {
            var record = await _context.IncidentRecords.FindAsync(id);
            if (record == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ." });

            if (record.Status == "CLOSED" || record.Status == "CANCELLED")
                return Json(new { success = false, message = "Không thể thêm người chứng kiến vào hồ sơ đã đóng hoặc hủy." });

            if (string.IsNullOrWhiteSpace(witnessName))
                return Json(new { success = false, message = "Tên người chứng kiến không được trống." });

            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var witness = new IncidentWitness
            {
                IncidentRecordId = record.Id,
                WitnessName = witnessName,
                Department = department,
                Notes = notes
            };
            _context.IncidentWitnesses.Add(witness);

            var history = new IncidentHistory
            {
                IncidentRecordId = record.Id,
                Timestamp = DateTime.Now,
                Actor = currentUser,
                Action = "ADD_WITNESS",
                Details = $"Thêm người chứng kiến: {witnessName}"
            };
            _context.IncidentHistories.Add(history);

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
