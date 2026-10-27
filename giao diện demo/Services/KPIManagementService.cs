using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Authorization;

namespace giao_dien_demo.Services
{
    public class KPIManagementService : IKPIManagementService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IManagerScopeService _managerScopeService;
        private readonly IAuditLogService _auditLogService;
        private readonly NotificationEngineService _notificationEngine;

        public KPIManagementService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            IManagerScopeService managerScopeService,
            IAuditLogService auditLogService,
            NotificationEngineService notificationEngine)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _managerScopeService = managerScopeService;
            _auditLogService = auditLogService;
            _notificationEngine = notificationEngine;
        }

        private string? GetSessionValue(string key) => _httpContextAccessor.HttpContext?.Session?.GetString(key);
        
        private string GetCurrentUsername() => GetSessionValue("User") ?? GetSessionValue("Username") ?? "System";

        private User? GetCurrentUser()
        {
            var username = GetCurrentUsername();
            return _context.Users.FirstOrDefault(u => u.Username == username);
        }

        private bool IsHrOnly()
        {
            var role = GetSessionValue("Role");
            return role == "HR";
        }

        // ==========================================
        // 1. PERIOD MANAGEMENT
        // ==========================================

        public async Task<KPIPeriod> CreatePeriodAsync(KPIPeriod period)
        {
            if (!IsHrOnly()) throw new UnauthorizedAccessException("Only HR can create KPI Periods.");

            if (string.IsNullOrWhiteSpace(period.Name)) throw new ArgumentException("Name is required.");
            if (period.StartDate >= period.EndDate) throw new ArgumentException("StartDate must be before EndDate.");
            
            period.Status = "PLANNED";
            period.CreatedAt = DateTime.Now;
            period.CreatedBy = GetCurrentUsername();

            _context.KPIPeriods.Add(period);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync("Create", "KPI", "KPIPeriod", period.Id.ToString(), "KPI", "Success");
            return period;
        }

        public async Task<bool> ActivatePeriodAsync(int periodId)
        {
            if (!IsHrOnly()) throw new UnauthorizedAccessException("Only HR can activate KPI Periods.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            
            var period = await _context.KPIPeriods.FirstOrDefaultAsync(p => p.Id == periodId);
            if (period == null) throw new KeyNotFoundException("Period not found.");

            if (period.Status != "PLANNED") throw new InvalidOperationException("Only PLANNED periods can be activated.");

            period.Status = "ACTIVE";
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await _auditLogService.LogAsync("Activate", "KPI", "KPIPeriod", period.Id.ToString(), "KPI", "Success");
            return true;
        }

        public async Task<(bool Success, int OutstandingDraft, int OutstandingActive, int OutstandingEvaluating)> ClosePeriodAsync(int periodId)
        {
            if (!IsHrOnly()) throw new UnauthorizedAccessException("Only HR can close KPI Periods.");

            using var transaction = await _context.Database.BeginTransactionAsync();

            var period = await _context.KPIPeriods.FirstOrDefaultAsync(p => p.Id == periodId);
            if (period == null) throw new KeyNotFoundException("Period not found.");
            if (period.Status != "ACTIVE") throw new InvalidOperationException("Only ACTIVE periods can be closed.");

            var evals = await _context.KPIEvaluations.Where(e => e.PeriodId == periodId).ToListAsync();
            int outstandingDraft = evals.Count(e => e.Status == "DRAFT");
            int outstandingActive = evals.Count(e => e.Status == "ACTIVE");
            int outstandingEvaluating = evals.Count(e => e.Status == "EVALUATING");

            period.Status = "CLOSED";
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await _auditLogService.LogAsync("Close", "KPI", "KPIPeriod", period.Id.ToString(), "KPI", "Success");
            
            return (true, outstandingDraft, outstandingActive, outstandingEvaluating);
        }

        // ==========================================
        // 2. EVALUATION MANAGEMENT
        // ==========================================

        public async Task<KPIEvaluation> CreateEvaluationAsync(int periodId, int employeeId)
        {
            var user = GetCurrentUser();
            if (user == null || user.Role != AppRoles.Manager) 
                throw new UnauthorizedAccessException("Only managers with active scope can create KPI evaluations.");

            var period = await _context.KPIPeriods.FindAsync(periodId);
            if (period == null) throw new KeyNotFoundException("Period not found.");
            if (period.Status != "PLANNED" && period.Status != "ACTIVE") 
                throw new InvalidOperationException("Cannot create evaluation in this period state.");

            // Scope check
            var employee = await _managerScopeService.GetScopedEmployeeQuery().FirstOrDefaultAsync(e => e.Id == employeeId);
            if (employee == null) throw new UnauthorizedAccessException("Employee is not in your active Manager assignment scope.");
            if (employee.Status == "Offboarded" || employee.Status == "Terminated" || employee.Status == "Suspended")
                throw new InvalidOperationException("Employee is not active.");

            // Unique check
            bool exists = await _context.KPIEvaluations.AnyAsync(e => e.PeriodId == periodId && e.EmployeeId == employeeId);
            if (exists) throw new InvalidOperationException("An evaluation already exists for this Employee and Period.");

            var eval = new KPIEvaluation
            {
                PeriodId = periodId,
                EmployeeId = employeeId,
                ManagerUserId = user.Id,
                ManagerUsernameSnapshot = user.Username,
                Status = "DRAFT",
                EmployeeDepartmentSnapshot = employee.Department,
                EmployeePositionSnapshot = employee.JobTitle,
                CreatedAt = DateTime.Now,
                CreatedBy = user.Username
            };

            _context.KPIEvaluations.Add(eval);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync("Create", "KPI", "KPIEvaluation", eval.Id.ToString(), "KPI", "Success");
            return eval;
        }

        private async Task<(KPIEvaluation Eval, User Manager)> GetOwnedEvaluationAsync(int evaluationId)
        {
            var user = GetCurrentUser();
            if (user == null || user.Role != AppRoles.Manager) 
                throw new UnauthorizedAccessException("Only managers can mutate evaluations.");

            var eval = await _context.KPIEvaluations
                .Include(e => e.Period)
                .Include(e => e.Targets)
                .Include(e => e.Employee)
                .FirstOrDefaultAsync(e => e.Id == evaluationId);

            if (eval == null) throw new KeyNotFoundException("Evaluation not found.");

            // Must be owner
            if (eval.ManagerUserId != user.Id)
                throw new UnauthorizedAccessException("You are not the original owner of this evaluation.");

            // Employee must still be in scope
            bool inScope = await _managerScopeService.GetScopedEmployeeQuery().AnyAsync(e => e.Id == eval.EmployeeId);
            if (!inScope)
                throw new UnauthorizedAccessException("The employee is no longer in your active managerial scope.");

            return (eval, user);
        }

        private void DetachPendingOutboxMessages()
        {
            var pending = _context.ChangeTracker.Entries<EventOutboxMessage>()
                .Where(e => e.State == EntityState.Added).ToList();
            foreach (var entry in pending)
            {
                entry.State = EntityState.Detached;
            }
        }

        public async Task<bool> ActivateEvaluationAsync(int evaluationId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var (eval, _) = await GetOwnedEvaluationAsync(evaluationId);

                if (eval.Status != "DRAFT") throw new InvalidOperationException("Only DRAFT evaluation can be activated.");
                if (eval.Period!.Status != "ACTIVE") throw new InvalidOperationException("Period must be ACTIVE to activate evaluation.");
                if (!eval.Targets.Any()) throw new InvalidOperationException("Evaluation must have at least one target.");

                decimal sum = eval.Targets.Sum(t => t.Weight);
                if (sum != 100.00m) throw new InvalidOperationException("Total weights must equal exactly 100.00.");

                eval.Status = "ACTIVE";
                await _context.SaveChangesAsync();
                
                // Queue Notification
                _notificationEngine.QueueEvent(_context, "KPI_EVALUATION_ACTIVATED", new { evalId = eval.Id }, eval.Employee!.EmployeeCode);

                await transaction.CommitAsync();

                await _auditLogService.LogAsync("Activate", "KPI", "KPIEvaluation", eval.Id.ToString(), "KPI", "Success");
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                DetachPendingOutboxMessages();
                throw;
            }
        }

        public async Task<bool> StartEvaluatingAsync(int evaluationId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            var (eval, _) = await GetOwnedEvaluationAsync(evaluationId);

            if (eval.Status != "ACTIVE") throw new InvalidOperationException("Only ACTIVE evaluation can be started for scoring.");
            // Frozen design: ACTIVE period allows this. If CLOSED, we also allow to finish up active evaluations.
            if (eval.Period!.Status == "PLANNED") throw new InvalidOperationException("Period is PLANNED.");

            eval.Status = "EVALUATING";
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await _auditLogService.LogAsync("StartEvaluating", "KPI", "KPIEvaluation", eval.Id.ToString(), "KPI", "Success");
            return true;
        }

        public async Task<bool> FinalizeEvaluationAsync(int evaluationId, string finalRating)
        {
            // Transaction Serializable for safety
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                // Re-fetch securely
                var (eval, _) = await GetOwnedEvaluationAsync(evaluationId);

                if (eval.Status != "EVALUATING") throw new InvalidOperationException("Only EVALUATING status can be finalized.");
                
                // Validate config
                var kpiConfig = await _context.KPIConfigs.FirstOrDefaultAsync(c => c.KPIRating == finalRating.Trim());
                if (kpiConfig == null) throw new ArgumentException("Invalid FinalRating. Must match KPIConfig.");

                decimal totalWeight = eval.Targets.Sum(t => t.Weight);
                if (totalWeight != 100.00m) throw new InvalidOperationException("Weights must equal exactly 100.00.");

                if (eval.Targets.Any(t => t.ManagerScore == null))
                    throw new InvalidOperationException("All targets must have a ManagerScore.");

                // Calculate final score
                decimal score = eval.Targets.Sum(t => t.ManagerScore!.Value * t.Weight / 100m);
                eval.FinalScore = Math.Round(score, 2, MidpointRounding.AwayFromZero);
                eval.FinalRating = kpiConfig.KPIRating;

                eval.Status = "COMPLETED";
                eval.CompletedAt = DateTime.Now;
                eval.CompletedBy = GetCurrentUsername();

                // Synchronize to Employee.KPIScore
                var employee = await _context.Employees.FindAsync(eval.EmployeeId);
                if (employee != null)
                {
                    employee.KPIScore = eval.FinalRating;
                }

                _notificationEngine.QueueEvent(_context, "KPI_EVALUATION_COMPLETED", new { evalId = eval.Id }, employee?.EmployeeCode);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _auditLogService.LogAsync("Finalize", "KPI", "KPIEvaluation", eval.Id.ToString(), "KPI", "Success");
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                DetachPendingOutboxMessages();
                throw;
            }
        }

        public async Task<bool> CancelEvaluationAsync(int evaluationId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            var eval = await _context.KPIEvaluations.FindAsync(evaluationId);
            if (eval == null) throw new KeyNotFoundException("Evaluation not found.");

            if (eval.Status == "COMPLETED" || eval.Status == "CANCELLED")
                throw new InvalidOperationException("Cannot cancel COMPLETED or CANCELLED evaluation.");

            // HR override or Manager owner check
            if (!IsHrOnly())
            {
                var user = GetCurrentUser();
                if (user == null || eval.ManagerUserId != user.Id)
                    throw new UnauthorizedAccessException("Only HR or Owner can cancel.");
                
                // Manager check scope
                bool inScope = await _managerScopeService.GetScopedEmployeeQuery().AnyAsync(e => e.Id == eval.EmployeeId);
                if (!inScope) throw new UnauthorizedAccessException("Employee is no longer in scope.");
            }

            eval.Status = "CANCELLED";
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await _auditLogService.LogAsync("Cancel", "KPI", "KPIEvaluation", eval.Id.ToString(), "KPI", "Success");
            return true;
        }

        // ==========================================
        // 3. TARGET MANAGEMENT
        // ==========================================

        public async Task<KPITarget> AddTargetAsync(int evaluationId, KPITarget target)
        {
            var (eval, _) = await GetOwnedEvaluationAsync(evaluationId);
            if (eval.Status != "DRAFT") throw new InvalidOperationException("Can only add targets to DRAFT evaluation.");

            if (string.IsNullOrWhiteSpace(target.Title)) throw new ArgumentException("Title required.");
            if (target.Weight <= 0 || target.Weight > 100) throw new ArgumentException("Weight must be 0 < Weight <= 100.");

            target.EvaluationId = evaluationId;
            target.Progress = 0;
            target.ManagerScore = null;
            target.CreatedAt = DateTime.Now;

            _context.KPITargets.Add(target);
            await _context.SaveChangesAsync();
            return target;
        }

        public async Task<bool> UpdateTargetAsync(int targetId, string title, string? description, decimal weight)
        {
            var target = await _context.KPITargets.Include(t => t.Evaluation).FirstOrDefaultAsync(t => t.Id == targetId);
            if (target == null) throw new KeyNotFoundException("Target not found.");

            // Reuse ownership check
            var (eval, _) = await GetOwnedEvaluationAsync(target.EvaluationId);
            if (eval.Status != "DRAFT") throw new InvalidOperationException("Can only update targets on DRAFT evaluation.");

            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title required.");
            if (weight <= 0 || weight > 100) throw new ArgumentException("Weight must be 0 < Weight <= 100.");

            target.Title = title;
            target.Description = description;
            target.Weight = weight;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveTargetAsync(int targetId)
        {
            var target = await _context.KPITargets.Include(t => t.Evaluation).FirstOrDefaultAsync(t => t.Id == targetId);
            if (target == null) throw new KeyNotFoundException("Target not found.");

            var (eval, _) = await GetOwnedEvaluationAsync(target.EvaluationId);
            if (eval.Status != "DRAFT") throw new InvalidOperationException("Can only remove targets from DRAFT evaluation.");

            _context.KPITargets.Remove(target);
            await _context.SaveChangesAsync();
            return true;
        }

        // ==========================================
        // 4. SCORING
        // ==========================================

        public async Task<bool> UpdateEmployeeProgressAsync(int targetId, decimal progress)
        {
            var user = GetCurrentUser();
            if (user == null || user.EmployeeId == null) throw new UnauthorizedAccessException("Must be employee.");

            var target = await _context.KPITargets
                .Include(t => t.Evaluation)
                .ThenInclude(e => e!.Period)
                .FirstOrDefaultAsync(t => t.Id == targetId);

            if (target == null) throw new KeyNotFoundException("Target not found.");

            var eval = target.Evaluation!;
            if (eval.EmployeeId != user.EmployeeId) throw new UnauthorizedAccessException("This is not your evaluation.");
            if (eval.Status != "ACTIVE") throw new InvalidOperationException("Can only update progress when ACTIVE.");
            if (eval.Period!.Status != "ACTIVE") throw new InvalidOperationException("Period must be ACTIVE to update progress.");

            if (progress < 0 || progress > 100) throw new ArgumentException("Progress must be 0-100.");

            target.Progress = progress;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateManagerScoreAsync(int targetId, decimal score)
        {
            var target = await _context.KPITargets.Include(t => t.Evaluation).FirstOrDefaultAsync(t => t.Id == targetId);
            if (target == null) throw new KeyNotFoundException("Target not found.");

            var (eval, _) = await GetOwnedEvaluationAsync(target.EvaluationId);
            if (eval.Status != "EVALUATING") throw new InvalidOperationException("Can only score when EVALUATING.");
            
            if (score < 0 || score > 100) throw new ArgumentException("Score must be 0-100.");

            target.ManagerScore = score;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
