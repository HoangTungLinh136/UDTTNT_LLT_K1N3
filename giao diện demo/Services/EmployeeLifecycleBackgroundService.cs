using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;

namespace giao_dien_demo.Services
{
    public class EmployeeLifecycleBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EmployeeLifecycleBackgroundService> _logger;

        public EmployeeLifecycleBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<EmployeeLifecycleBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("EmployeeLifecycleBackgroundService is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessLifecycleEventsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing EmployeeLifecycleBackgroundService cycle.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("EmployeeLifecycleBackgroundService is stopping.");
        }

        private async Task ProcessLifecycleEventsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var appointmentService = scope.ServiceProvider.GetRequiredService<IAppointmentEffectiveService>();
            var transferService = scope.ServiceProvider.GetRequiredService<ITransferEffectiveService>();
            var dismissalService = scope.ServiceProvider.GetRequiredService<IDismissalEffectiveService>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // 1. Process Appointments via AppointmentEffectiveService
            try
            {
                await appointmentService.ProcessPendingAppointmentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pending appointments in background service.");
            }

            // 2. Process Transfers via TransferEffectiveService
            try
            {
                await transferService.ProcessPendingTransfersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pending transfers in background service.");
            }

            // 3. Process Dismissals via DismissalEffectiveService (Miễn nhiệm an toàn, không terminate)
            try
            {
                await dismissalService.ProcessPendingDismissalsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pending dismissals in background service.");
            }

            // 4. Process Offboarding (Preserved offboarding workflow only)
            try
            {
                var today = DateTime.Today;
                var pendingOffboardings = await context.OffboardingRecords
                    .Where(o => o.ApprovalStatus == "APPROVED" && o.EffectiveDate <= today && o.CompletedAt == null)
                    .ToListAsync(stoppingToken);

                foreach (var o in pendingOffboardings)
                {
                    var emp = await context.Employees.FindAsync(new object[] { o.EmployeeId }, stoppingToken);
                    if (emp != null)
                    {
                        emp.Status = "Offboarded";

                        var user = await context.Users.FirstOrDefaultAsync(u => u.EmployeeId == emp.Id, stoppingToken);
                        if (user != null)
                        {
                            user.CanLogin = false;
                        }

                        o.CompletedAt = DateTime.Now;
                        o.CompletedBy = "SYSTEM";
                    }
                }

                if (pendingOffboardings.Any())
                {
                    await context.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing offboarding records in background service.");
            }
        }
    }
}
