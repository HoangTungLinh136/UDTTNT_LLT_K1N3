using System;
using System.Threading.Tasks;
using giao_dien_demo.Models;

namespace giao_dien_demo.Services
{
    public class DirectManagerChangeRequest
    {
        public int EmployeeId { get; set; }
        public int? NewManagerId { get; set; }
        public bool AllowNullManagerException { get; set; }
        public string? NullManagerReason { get; set; }
        public string? Reason { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    public class DirectManagerChangeResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public EmployeeReportingHistory? History { get; set; }
    }

    public interface IDirectManagerService
    {
        Task<DirectManagerChangeResult> ChangeDirectManagerAsync(
            DirectManagerChangeRequest request,
            string actor,
            int? actorUserId = null);
    }
}

