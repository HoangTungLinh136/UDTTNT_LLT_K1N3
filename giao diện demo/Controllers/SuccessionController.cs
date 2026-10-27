using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace giao_dien_demo.Controllers
{
    [Authorize(Roles = $"{AppRoles.HR},{AppRoles.Leader}")]
    [AutoValidateAntiforgeryToken]
    public class SuccessionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogService _auditLog;
        private readonly ILeadershipScopeService _leadershipScope;

        public SuccessionController(
            ApplicationDbContext context,
            IAuditLogService auditLog,
            ILeadershipScopeService leadershipScope)
        {
            _context = context;
            _auditLog = auditLog;
            _leadershipScope = leadershipScope;
        }

        private string GetCurrentUsername()
        {
            return CurrentActor.Username(User) ?? "SYSTEM";
        }

        // --- Views ---
        public async Task<IActionResult> Index()
        {
            var role = CurrentActor.Role(User);
            var isLeader = role == AppRoles.Leader;
            var leaderUnits = isLeader ? _leadershipScope.GetOverseenOrganizationalUnits() : new List<string>();

            var currentPeriod = await _context.PlanningPeriods
                .Where(p => p.Status == "ACTIVE")
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            int targetPositionsCount = 0;
            int potentialCandidatesCount = 0;
            int readyNowCount = 0;
            int devRequiredCount = 0;

            var allPeriods = await _context.PlanningPeriods.OrderByDescending(p => p.StartDate).ToListAsync();

            if (currentPeriod != null)
            {
                var plansQuery = _context.SuccessionPlans
                    .Where(p => p.PlanningPeriodId == currentPeriod.Id);

                // Scope for Leader: strictly within active LeaderAssignment units
                if (isLeader)
                {
                    if (leaderUnits.Count == 0)
                    {
                        plansQuery = plansQuery.Where(p => false);
                    }
                    else
                    {
                        plansQuery = plansQuery.Where(p => leaderUnits.Contains(p.Department));
                    }
                }

                var plansInPeriod = await plansQuery.ToListAsync();
                targetPositionsCount = plansInPeriod.Count;
                var planIds = plansInPeriod.Select(p => p.Id).ToList();

                var candidates = await _context.SuccessionCandidates
                    .Where(c => planIds.Contains(c.SuccessionPlanId))
                    .ToListAsync();

                potentialCandidatesCount = candidates.Select(c => c.EmployeeId).Distinct().Count();
                readyNowCount = candidates.Count(c => c.ReadinessLevel == "READY_NOW");
                devRequiredCount = candidates.Count(c => c.ReadinessLevel != "READY_NOW" && !string.IsNullOrEmpty(c.ReadinessLevel));
            }

            ViewBag.TargetPositionsCount = targetPositionsCount;
            ViewBag.PotentialCandidatesCount = potentialCandidatesCount;
            ViewBag.ReadyNowCount = readyNowCount;
            ViewBag.DevRequiredCount = devRequiredCount;
            
            ViewBag.Periods = allPeriods;
            ViewBag.CurrentPeriodId = currentPeriod?.Id;
            
            var jobPositions = await _context.JobPositions.ToListAsync();
            ViewBag.JobPositions = jobPositions;

            return View();
        }

        // --- API: Get Plans (scoped) ---
        [HttpGet]
        public async Task<IActionResult> GetPlans(int? periodId, string? department, string? readiness)
        {
            var role = CurrentActor.Role(User);
            var isLeader = role == AppRoles.Leader;
            var leaderUnits = isLeader ? _leadershipScope.GetOverseenOrganizationalUnits() : new List<string>();

            // If Leader has no active assignments, return zero records (never all data!)
            if (isLeader && leaderUnits.Count == 0)
            {
                return Json(new List<object>());
            }

            var query = from p in _context.SuccessionPlans
                        join jp in _context.JobPositions on p.TargetPositionId equals jp.Id
                        select new { p, jp };

            if (isLeader)
            {
                query = query.Where(x => leaderUnits.Contains(x.p.Department));
            }

            if (periodId.HasValue)
            {
                query = query.Where(x => x.p.PlanningPeriodId == periodId.Value);
            }
            if (!string.IsNullOrEmpty(department))
            {
                if (isLeader && !leaderUnits.Contains(department))
                {
                    return Forbid();
                }
                query = query.Where(x => x.p.Department == department);
            }

            var plansData = await query.ToListAsync();
            var planIds = plansData.Select(x => x.p.Id).ToList();

            var candidates = await _context.SuccessionCandidates
                .Where(c => planIds.Contains(c.SuccessionPlanId))
                .ToListAsync();

            var result = plansData.Select(x => {
                var planCandidates = candidates.Where(c => c.SuccessionPlanId == x.p.Id).ToList();
                var count = planCandidates.Count;
                
                string bestReadiness = "Chưa đánh giá";
                if (planCandidates.Any(c => c.ReadinessLevel == "READY_NOW")) bestReadiness = "READY_NOW";
                else if (planCandidates.Any(c => c.ReadinessLevel == "READY_1_2_YEARS")) bestReadiness = "READY_1_2_YEARS";
                else if (planCandidates.Any(c => c.ReadinessLevel == "READY_2_3_YEARS")) bestReadiness = "READY_2_3_YEARS";
                else if (planCandidates.Any(c => c.ReadinessLevel == "DEVELOPMENT_REQUIRED")) bestReadiness = "DEVELOPMENT_REQUIRED";

                return new {
                    id = x.p.Id,
                    targetPosition = x.jp.Name,
                    department = x.p.Department,
                    candidatesCount = count,
                    priority = x.p.Priority,
                    bestReadiness = bestReadiness,
                    status = x.p.Status,
                    owner = x.p.OwnerName,
                    updatedAt = x.p.UpdatedAt.ToString("dd/MM/yyyy HH:mm")
                };
            }).ToList();

            if (!string.IsNullOrEmpty(readiness))
            {
                result = result.Where(x => x.bestReadiness == readiness).ToList();
            }

            return Json(result);
        }
        
        // --- API: Create Period (HR only) ---
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> CreatePeriod([FromBody] PlanningPeriod model)
        {
            model.Status = "ACTIVE";
            model.CreatedAt = DateTime.Now;
            _context.PlanningPeriods.Add(model);
            await _context.SaveChangesAsync();

            await _auditLog.LogAsync("CreatePeriod", "Succession", "SuccessionCore", model.Id.ToString(), "Create", "Success");

            return Json(new { success = true, id = model.Id });
        }

        // --- API: Add Target Position (HR only) ---
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> AddTargetPosition([FromBody] SuccessionPlan model)
        {
            model.Status = "DRAFT";
            model.OwnerName = GetCurrentUsername();
            model.CreatedAt = DateTime.Now;
            model.UpdatedAt = DateTime.Now;
            _context.SuccessionPlans.Add(model);
            await _context.SaveChangesAsync();
            
            _context.SuccessionHistories.Add(new SuccessionHistory {
                SuccessionPlanId = model.Id,
                Action = "ADD_TARGET_POSITION",
                Actor = GetCurrentUsername(),
                Details = "Thêm vị trí quy hoạch"
            });
            await _context.SaveChangesAsync();

            await _auditLog.LogAsync("AddTargetPosition", "Succession", "SuccessionCore", model.Id.ToString(), "Create", "Success");

            return Json(new { success = true, id = model.Id });
        }

        // --- API: Get Plan Details (scoped) ---
        [HttpGet]
        public async Task<IActionResult> GetPlanDetails(int id)
        {
            var plan = await _context.SuccessionPlans.FindAsync(id);
            if (plan == null) return NotFound();

            var role = CurrentActor.Role(User);
            if (role == AppRoles.Leader)
            {
                if (!_leadershipScope.CanAccessDepartment(plan.Department))
                {
                    return Forbid();
                }
            }

            var jp = await _context.JobPositions.FindAsync(plan.TargetPositionId);
            var period = await _context.PlanningPeriods.FindAsync(plan.PlanningPeriodId);

            var candidates = await _context.SuccessionCandidates
                .Where(c => c.SuccessionPlanId == id)
                .OrderBy(c => c.Rank ?? 999)
                .ToListAsync();

            return Json(new {
                plan = new {
                    id = plan.Id,
                    targetPosition = jp?.Name,
                    department = plan.Department,
                    priority = plan.Priority,
                    reason = plan.Reason,
                    owner = plan.OwnerName,
                    periodName = period?.Name,
                    status = plan.Status
                },
                candidates = candidates.Select(c => new {
                    id = c.Id,
                    rank = c.Rank,
                    employeeCode = c.EmployeeCodeSnapshot,
                    fullName = c.EmployeeNameSnapshot,
                    department = c.DepartmentSnapshot,
                    position = c.PositionSnapshot,
                    readiness = c.ReadinessLevel,
                    competencyScore = c.CompetencyScore,
                    performanceScore = c.PerformanceScore,
                    experienceScore = c.ExperienceScore,
                    potentialScore = c.PotentialScore
                })
            });
        }

        // --- API: Search Employees (HR only) ---
        [HttpGet]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> SearchEmployees(string query)
        {
            if (string.IsNullOrEmpty(query)) return Json(new object[] { });

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
                    position = e.Position
                })
                .ToListAsync();

            return Json(emps);
        }

        // --- API: Add Candidate (HR only) ---
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> AddCandidate([FromBody] SuccessionCandidate model)
        {
            // Duplicate check
            var exists = await _context.SuccessionCandidates
                .AnyAsync(c => c.SuccessionPlanId == model.SuccessionPlanId && c.EmployeeId == model.EmployeeId);
            
            if (exists) return Json(new { success = false, message = "Nhân viên này đã nằm trong danh sách quy hoạch của vị trí này." });

            var emp = await _context.Employees.FindAsync(model.EmployeeId);
            if (emp == null) return Json(new { success = false, message = "Không tìm thấy nhân viên." });

            model.EmployeeCodeSnapshot = emp.EmployeeCode;
            model.EmployeeNameSnapshot = emp.Name;
            model.DepartmentSnapshot = emp.Department;
            model.PositionSnapshot = emp.Position;
            model.AddedAt = DateTime.Now;

            _context.SuccessionCandidates.Add(model);
            await _context.SaveChangesAsync();

            _context.SuccessionHistories.Add(new SuccessionHistory {
                SuccessionPlanId = model.SuccessionPlanId,
                Action = "ADD_CANDIDATE",
                Actor = GetCurrentUsername(),
                Details = $"Đưa {emp.Name} vào danh sách ứng viên"
            });
            await _context.SaveChangesAsync();

            await _auditLog.LogAsync("AddCandidate", "Succession", "SuccessionCore", model.Id.ToString(), "Create", "Success");

            return Json(new { success = true, id = model.Id });
        }

        // --- API: Update Assessment (HR only) ---
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> UpdateAssessment([FromBody] SuccessionCandidate model)
        {
            var candidate = await _context.SuccessionCandidates.FindAsync(model.Id);
            if (candidate == null) return NotFound();

            candidate.CompetencyScore = model.CompetencyScore;
            candidate.PerformanceScore = model.PerformanceScore;
            candidate.ExperienceScore = model.ExperienceScore;
            candidate.PotentialScore = model.PotentialScore;
            candidate.ReadinessLevel = model.ReadinessLevel;
            candidate.Rank = model.Rank;
            candidate.AssessmentNotes = model.AssessmentNotes;

            _context.SuccessionCandidates.Update(candidate);
            await _context.SaveChangesAsync();

            _context.SuccessionHistories.Add(new SuccessionHistory {
                SuccessionPlanId = candidate.SuccessionPlanId,
                Action = "UPDATE_ASSESSMENT",
                Actor = GetCurrentUsername(),
                Details = $"Cập nhật đánh giá cho {candidate.EmployeeNameSnapshot}"
            });
            await _context.SaveChangesAsync();

            await _auditLog.LogAsync("UpdateAssessment", "Succession", "SuccessionCore", candidate.Id.ToString(), "Update", "Success");

            return Json(new { success = true });
        }

        // --- API: Get Development Plan (scoped) ---
        [HttpGet]
        public async Task<IActionResult> GetDevelopmentPlan(int candidateId)
        {
            var candidate = await _context.SuccessionCandidates.FindAsync(candidateId);
            if (candidate == null) return NotFound();

            var plan = await _context.SuccessionPlans.FindAsync(candidate.SuccessionPlanId);
            if (plan == null) return NotFound();

            var role = CurrentActor.Role(User);
            if (role == AppRoles.Leader)
            {
                if (!_leadershipScope.CanAccessDepartment(plan.Department))
                {
                    return Forbid();
                }
            }

            var devPlan = await _context.CandidateDevelopmentPlans
                .FirstOrDefaultAsync(dp => dp.SuccessionCandidateId == candidateId);
            
            if (devPlan == null) return Json(new { success = false });

            return Json(new { success = true, data = devPlan });
        }

        // --- API: Save Development Plan (HR only) ---
        [HttpPost]
        [Authorize(Policy = AppPolicies.HROnly)]
        public async Task<IActionResult> SaveDevelopmentPlan([FromBody] CandidateDevelopmentPlan model)
        {
            var candidate = await _context.SuccessionCandidates.FindAsync(model.SuccessionCandidateId);
            if (candidate == null) return NotFound();

            var devPlan = await _context.CandidateDevelopmentPlans
                .FirstOrDefaultAsync(dp => dp.SuccessionCandidateId == model.SuccessionCandidateId);

            if (devPlan == null)
            {
                model.Status = "IN_PROGRESS";
                _context.CandidateDevelopmentPlans.Add(model);
            }
            else
            {
                devPlan.Objective = model.Objective;
                devPlan.DevelopmentGap = model.DevelopmentGap;
                devPlan.Activities = model.Activities;
                devPlan.TargetDate = model.TargetDate;
                devPlan.ResponsiblePerson = model.ResponsiblePerson;
                devPlan.Progress = model.Progress;
                _context.CandidateDevelopmentPlans.Update(devPlan);
            }

            await _context.SaveChangesAsync();

            _context.SuccessionHistories.Add(new SuccessionHistory {
                SuccessionPlanId = candidate.SuccessionPlanId,
                Action = "UPDATE_DEVELOPMENT_PLAN",
                Actor = GetCurrentUsername(),
                Details = $"Cập nhật Kế hoạch phát triển cho {candidate.EmployeeNameSnapshot}"
            });
            await _context.SaveChangesAsync();

            await _auditLog.LogAsync("SaveDevelopmentPlan", "Succession", "SuccessionCore", candidate.Id.ToString(), "Update", "Success");

            return Json(new { success = true });
        }
        
        // --- API: Get History (scoped) ---
        [HttpGet]
        public async Task<IActionResult> GetHistory(int planId)
        {
            var plan = await _context.SuccessionPlans.FindAsync(planId);
            if (plan == null) return NotFound();

            var role = CurrentActor.Role(User);
            if (role == AppRoles.Leader)
            {
                if (!_leadershipScope.CanAccessDepartment(plan.Department))
                {
                    return Forbid();
                }
            }

            var history = await _context.SuccessionHistories
                .Where(h => h.SuccessionPlanId == planId)
                .OrderByDescending(h => h.Timestamp)
                .Select(h => new {
                    action = h.Action,
                    actor = h.Actor,
                    details = h.Details,
                    timestamp = h.Timestamp.ToString("dd/MM/yyyy HH:mm")
                })
                .ToListAsync();
            
            return Json(history);
        }
    }
}
