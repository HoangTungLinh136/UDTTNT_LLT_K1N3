using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using giao_dien_demo.Data;
using giao_dien_demo.Models;

namespace giao_dien_demo.Services
{
    public class OffboardingEffectiveService : BackgroundService
    {
        private readonly ILogger<OffboardingEffectiveService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public OffboardingEffectiveService(ILogger<OffboardingEffectiveService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OffboardingEffectiveService is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        await ProcessEffectiveOffboardings(context, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing OffboardingEffectiveService.");
                }

                // Check again every hour
                try { await Task.Delay(TimeSpan.FromHours(1), stoppingToken); } catch (System.OperationCanceledException) { break; }
            }

            _logger.LogInformation("OffboardingEffectiveService is stopping.");
        }

        private async Task ProcessEffectiveOffboardings(ApplicationDbContext context, CancellationToken stoppingToken)
        {
            var today = DateTime.Today;

            // Find all COMPLETED offboardings where EffectiveDate <= Today and the employee is still active (not "Offboarded")
            var effectiveOffboardings = await context.OffboardingRecords
                .Include(a => a.Employee)
                .Where(a => a.OffboardingStatus == "COMPLETED" && 
                            a.EffectiveDate.Date <= today &&
                            a.Employee != null &&
                            a.Employee.Status != "Offboarded")
                .ToListAsync(stoppingToken);

            if (effectiveOffboardings.Any())
            {
                _logger.LogInformation($"Found {effectiveOffboardings.Count} offboardings to apply.");

                foreach (var record in effectiveOffboardings)
                {
                    if (record.Employee != null)
                    {
                        record.Employee.Status = "Offboarded";

                        // Lock User account (Bảo vệ tài khoản quản trị hệ thống không bao giờ bị khóa tự động)
                        var userAccount = await context.Users.FirstOrDefaultAsync(u => u.EmployeeId == record.EmployeeId);
                        if (userAccount != null && userAccount.Username != "linh.admin" && userAccount.Role != "Admin") {
                            userAccount.CanLogin = false;
                        }

                        var history = new OffboardingHistory
                        {
                            OffboardingRecordId = record.Id,
                            Actor = "System_Scheduler",
                            Action = "APPLY",
                            Details = "Hệ thống tự động áp dụng trạng thái Offboarded khi đến ngày hiệu lực."
                        };
                        context.OffboardingHistories.Add(history);
                    }
                }

                await context.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Successfully applied offboardings.");
            }
        }
    }
}

