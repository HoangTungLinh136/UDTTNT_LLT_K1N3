using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using giao_dien_demo.Services;
using Microsoft.Extensions.DependencyInjection;
using giao_dien_demo.Data;
using giao_dien_demo.Models;

namespace giao_dien_demo.Middlewares
{
    public class SystemMonitoringMiddlewares
    {
        private readonly RequestDelegate _next;

        public SystemMonitoringMiddlewares(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ITelemetryStore telemetryStore, IServiceScopeFactory scopeFactory)
        {
            var sw = Stopwatch.StartNew();
            var isError = false;
            var correlationId = Guid.NewGuid().ToString("N");
            context.Items["CorrelationId"] = correlationId;

            try
            {
                await _next(context);
                if (context.Response.StatusCode >= 400)
                {
                    isError = true;
                }
            }
            catch (Exception ex)
            {
                isError = true;
                await LogExceptionAsync(context, ex, scopeFactory, correlationId);
                throw; // Rethrow to let global error handler catch it if needed
            }
            finally
            {
                sw.Stop();
                // Bỏ qua các file tĩnh
                var path = context.Request.Path.Value;
                if (!path.Contains("."))
                {
                    telemetryStore.RecordApiRequest(context.Request.Method, path, sw.ElapsedMilliseconds, isError);
                }
            }
        }

        private async Task LogExceptionAsync(HttpContext context, Exception ex, IServiceScopeFactory scopeFactory, string correlationId)
        {
            using (var scope = scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                var user = "";
                if (context.Features.Get<Microsoft.AspNetCore.Http.Features.ISessionFeature>()?.Session != null)
                {
                    try { user = context.Session.GetString("User") ?? ""; } catch { }
                }
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "";
                var ua = context.Request.Headers["User-Agent"].ToString();

                var errorLog = new SystemErrorLog
                {
                    Timestamp = DateTime.Now,
                    Level = "Error",
                    Service = "WebApp",
                    ExceptionType = ex.GetType().Name,
                    Message = ex.Message,
                    StackTrace = ex.StackTrace ?? "",
                    RequestId = context.TraceIdentifier,
                    CorrelationId = correlationId,
                    User = user,
                    IP = ip,
                    UserAgent = ua.Length > 500 ? ua.Substring(0, 500) : ua
                };

                try
                {
                    dbContext.SystemErrorLogs.Add(errorLog);
                    await dbContext.SaveChangesAsync();
                }
                catch
                {
                    // Logging failure must never hide the original application exception.
                    // SystemErrorLogs may not exist yet in a database with migration drift.
                }
            }
        }
    }
}

