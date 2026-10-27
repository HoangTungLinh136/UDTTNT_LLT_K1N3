using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using giao_dien_demo.Models.AI;

namespace giao_dien_demo.Services.AI
{
    public interface IUserMemoryContextProvider
    {
        Task<IReadOnlyList<UserMemoryContextItem>> GetForUserAsync(
            string userId,
            CancellationToken cancellationToken = default
        );
    }
}