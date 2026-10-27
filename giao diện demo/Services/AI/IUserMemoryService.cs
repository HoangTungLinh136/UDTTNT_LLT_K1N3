using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using giao_dien_demo.Models.AI;

namespace giao_dien_demo.Services.AI
{
    public interface IUserMemoryService
    {
        Task<UserMemory?> SaveCandidateAsync(
            string userId,
            string? employeeId,
            string? conversationId,
            MemoryCandidate candidate,
            CancellationToken cancellationToken = default
        );

        Task<IReadOnlyList<UserMemory>> GetActiveMemoriesForUserAsync(
            string userId,
            int limit = 20,
            CancellationToken cancellationToken = default
        );

        Task<IReadOnlyList<UserMemory>> GetActiveMemoriesForUserByTypeAsync(
            string userId,
            string memoryType,
            int limit = 20,
            CancellationToken cancellationToken = default
        );

        Task<bool> DeactivateMemoryAsync(
            string userId,
            long memoryId,
            CancellationToken cancellationToken = default
        );
    }
}