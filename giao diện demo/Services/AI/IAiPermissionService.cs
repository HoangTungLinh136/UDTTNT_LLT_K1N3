using giao_dien_demo.Models;
using giao_dien_demo.Models.AI;

namespace giao_dien_demo.Services.AI;

public interface IAiPermissionService
{
    Task<AvaUserContext> GetContextAsync(User user, CancellationToken cancellationToken = default);
}
