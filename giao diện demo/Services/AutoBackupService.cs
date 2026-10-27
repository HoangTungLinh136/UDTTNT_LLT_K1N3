using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using giao_dien_demo.Data;

namespace giao_dien_demo.Services
{
    public class AutoBackupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AutoBackupService> _logger;

        public AutoBackupService(IServiceProvider serviceProvider, ILogger<AutoBackupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AutoBackupService is starting.");

            // Không giữ ASP.NET host startup trong lần kiểm tra backup đầu tiên.
            // Nhường execution để Kestrel có thể bind port trước khi background work chạy.
            await Task.Yield();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndRunBackupAsync();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing AutoBackupService.");
                }

                // Check every 1 minute
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("AutoBackupService is stopping.");
        }

        private async Task CheckAndRunBackupAsync()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();

                var config = context.BackupConfigs.OrderBy(b => b.Id).FirstOrDefault();
                if (config == null || !config.IsEnabled)
                    return;

                var now = DateTime.Now;
                var currentTime = now.TimeOfDay;

                // Check if it's time to run
                // Assuming ExecuteTime is set to a specific HH:mm, we give it a 2-minute window
                if (currentTime >= config.ExecuteTime && currentTime <= config.ExecuteTime.Add(TimeSpan.FromMinutes(2)))
                {
                    // Check frequency
                    bool shouldRun = false;
                    if (config.LastRunTime == null)
                    {
                        shouldRun = true;
                    }
                    else
                    {
                        var lastRunDate = config.LastRunTime.Value.Date;
                        if (config.Frequency == "Daily" && lastRunDate < now.Date)
                        {
                            shouldRun = true;
                        }
                        else if (config.Frequency == "Weekly" && (now.Date - lastRunDate).TotalDays >= 7)
                        {
                            shouldRun = true;
                        }
                        else if (config.Frequency == "Monthly" && (now.Date - lastRunDate).TotalDays >= 28) // Simplistic monthly check
                        {
                            shouldRun = true;
                        }
                    }

                    if (shouldRun)
                    {
                        _logger.LogInformation("Starting scheduled backup...");
                        try
                        {
                            await backupService.CreateBackupAsync(config.BackupType ?? "Full", "System");
                            
                            config.LastRunTime = now;
                            context.Update(config);
                            await context.SaveChangesAsync();

                            // Process Retention Policy
                            if (config.RetentionDays > 0)
                            {
                                var cutoffDate = now.AddDays(-config.RetentionDays);
                                var oldBackups = context.BackupRecords
                                    .Where(b => b.CreatedAt < cutoffDate && !b.IsDeleted)
                                    .ToList();

                                foreach (var oldBackup in oldBackups)
                                {
                                    await backupService.DeleteBackupAsync(oldBackup.Id, "System");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Scheduled backup failed.");
                        }
                    }
                }
            }
        }
    }
}

