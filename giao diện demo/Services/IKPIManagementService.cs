using System.Collections.Generic;
using System.Threading.Tasks;
using giao_dien_demo.Models;

namespace giao_dien_demo.Services
{
    public interface IKPIManagementService
    {
        // Period Management
        Task<KPIPeriod> CreatePeriodAsync(KPIPeriod period);
        Task<bool> ActivatePeriodAsync(int periodId);
        Task<(bool Success, int OutstandingDraft, int OutstandingActive, int OutstandingEvaluating)> ClosePeriodAsync(int periodId);

        // Evaluation Management
        Task<KPIEvaluation> CreateEvaluationAsync(int periodId, int employeeId);
        Task<bool> ActivateEvaluationAsync(int evaluationId);
        Task<bool> StartEvaluatingAsync(int evaluationId);
        Task<bool> FinalizeEvaluationAsync(int evaluationId, string finalRating);
        Task<bool> CancelEvaluationAsync(int evaluationId);

        // Target Management
        Task<KPITarget> AddTargetAsync(int evaluationId, KPITarget target);
        Task<bool> UpdateTargetAsync(int targetId, string title, string? description, decimal weight);
        Task<bool> RemoveTargetAsync(int targetId);

        // Scoring (Employee / Manager)
        Task<bool> UpdateEmployeeProgressAsync(int targetId, decimal progress);
        Task<bool> UpdateManagerScoreAsync(int targetId, decimal score);
    }
}
