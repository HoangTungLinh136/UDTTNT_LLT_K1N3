using System.Threading.Tasks;
using giao_dien_demo.Data;
using giao_dien_demo.Models;

namespace giao_dien_demo.Services
{
    public interface IWorkflowPolicy<T> where T : class, IApprovableEntity
    {
        string DomainName { get; }
        
        // Return true if the user is authorized to approve this specific entity
        Task<bool> IsEligibleApproverAsync(T entity, string approverUsername, string approverRole, ApplicationDbContext context);
        
        // Execute downstream side-effects upon approval (e.g. recalculate attendance)
        Task OnApprovedAsync(T entity, ApplicationDbContext context);
        
        // Execute downstream side-effects upon rejection
        Task OnRejectedAsync(T entity, ApplicationDbContext context);

        Task OnRevisionRequestedAsync(T entity, ApplicationDbContext context) => Task.CompletedTask;
    }
}
