using System.Security.Claims;
using System.Threading.Tasks;
using giao_dien_demo.Models;

namespace giao_dien_demo.Services
{
    public interface IOrganizationHierarchyService
    {
        Task<OrganizationHierarchyViewModel> BuildHierarchyAsync(ClaimsPrincipal userPrincipal, string? departmentFilter = null);
    }
}

