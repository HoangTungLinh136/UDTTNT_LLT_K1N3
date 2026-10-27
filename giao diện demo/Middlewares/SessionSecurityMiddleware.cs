using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System;
using System.Threading.Tasks;
using giao_dien_demo.Services;

namespace giao_dien_demo.Middlewares
{
    public class SessionSecurityMiddleware
    {
        private readonly RequestDelegate _next;

        public SessionSecurityMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var sessionUser = context.Session.GetString("User");
            var principal = context.User;
            var authenticated = principal?.Identity?.IsAuthenticated == true;

            if (!authenticated)
            {
                // A session key must never authenticate a request by itself.
                if (!string.IsNullOrEmpty(sessionUser))
                    context.Session.Clear();

                await _next(context);
                return;
            }

            var username = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = principal.FindFirstValue(ClaimTypes.Role);
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(role))
            {
                await ExpireSessionAsync(context);
                return;
            }

            var settings = SecuritySettingsService.GetSettings();
            var timeout = TimeSpan.FromMinutes(settings.SessionIdleTimeoutMinutes);
            var sessionMatchesPrincipal = string.Equals(sessionUser, username, StringComparison.Ordinal);

            if (sessionMatchesPrincipal)
            {
                var lastActivityValue = context.Session.GetString("LastActivity");
                if (!string.IsNullOrWhiteSpace(lastActivityValue)
                    && DateTime.TryParse(lastActivityValue, out var lastActivity)
                    && DateTime.Now - lastActivity > timeout)
                {
                    await ExpireSessionAsync(context);
                    return;
                }
            }
            else
            {
                // In-memory session data can disappear during an app restart.
                // Restore only from the validated cookie principal, and only
                // while its issue time remains within the configured idle window.
                var issuedUtc = context.Features
                    .Get<IAuthenticateResultFeature>()?
                    .AuthenticateResult?
                    .Properties?
                    .IssuedUtc;

                if (!issuedUtc.HasValue || DateTimeOffset.UtcNow - issuedUtc.Value > timeout)
                {
                    await ExpireSessionAsync(context);
                    return;
                }

                context.Session.Clear();
                context.Session.SetString("User", username);
                context.Session.SetString("Username", username);
                context.Session.SetString("Role", role);
                context.Session.SetString("RealName", principal.FindFirstValue(ClaimTypes.Name) ?? username);

                var employeeIdClaim = principal.FindFirstValue("EmployeeId");
                if (int.TryParse(employeeIdClaim, out var employeeId))
                    context.Session.SetInt32("EmployeeId", employeeId);
            }

            // Keep the legacy state synchronized with the authenticated
            // principal; controllers must not treat browser/session data as identity.
            context.Session.SetString("Role", role);
            context.Session.SetString("LastActivity", DateTime.Now.ToString("O"));

            await _next(context);
        }

        private static async Task ExpireSessionAsync(HttpContext context)
        {
            context.Session.Clear();
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Response.Redirect("/Account/Login");
        }
    }
}
