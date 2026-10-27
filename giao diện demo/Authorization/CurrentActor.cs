using System.Security.Claims;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using Microsoft.EntityFrameworkCore;

namespace giao_dien_demo.Authorization;

public static class CurrentActor
{
    public static string? Username(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier);

    public static string? Role(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Role);

    public static async Task<User?> ResolveUserAsync(ClaimsPrincipal principal, ApplicationDbContext context)
    {
        if (principal.Identity?.IsAuthenticated != true) return null;
        var username = Username(principal);
        if (string.IsNullOrWhiteSpace(username)) return null;

        return await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Username == username && x.CanLogin);
    }
}
