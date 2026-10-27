using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;

namespace giao_dien_demo.Services
{
    public class NotificationBackgroundWorker : BackgroundService
    {
        private readonly ILogger<NotificationBackgroundWorker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private const int MaxRetries = 3;
        private const int DelayMilliseconds = 10000; // 10 seconds

        public NotificationBackgroundWorker(ILogger<NotificationBackgroundWorker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NotificationBackgroundWorker is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingMessagesAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing NotificationBackgroundWorker.");
                }

                try
                {
                    await Task.Delay(DelayMilliseconds, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("NotificationBackgroundWorker is stopping.");
        }

        private async Task ProcessPendingMessagesAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var notificationEngine = scope.ServiceProvider.GetRequiredService<NotificationEngineService>();

            var timeoutThreshold = DateTime.Now.AddMinutes(-5);
            var pendingMessages = await dbContext.EventOutboxMessages
                .Where(m => (m.Status == "PENDING" || (m.Status == "PROCESSING" && m.LastAttemptAt < timeoutThreshold)) && m.RetryCount < MaxRetries)
                .OrderBy(m => m.CreatedAt)
                .Take(50)
                .ToListAsync(stoppingToken);

            if (!pendingMessages.Any()) return;

            foreach (var message in pendingMessages)
            {
                var now = DateTime.Now;
                
                // Atomic claim to prevent multi-worker race conditions
                // Separate normal claim from stale PROCESSING recovery
                var rowsAffected = await dbContext.EventOutboxMessages
                    .Where(m => m.Id == message.Id && m.Status == "PENDING")
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.Status, "PROCESSING")
                        .SetProperty(p => p.LastAttemptAt, now), stoppingToken);

                if (rowsAffected == 0)
                {
                    // Attempt stale PROCESSING recovery
                    rowsAffected = await dbContext.EventOutboxMessages
                        .Where(m => m.Id == message.Id && 
                               m.Status == "PROCESSING" && 
                               m.LastAttemptAt < timeoutThreshold)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(p => p.Status, "PROCESSING")
                            .SetProperty(p => p.LastAttemptAt, now), stoppingToken);

                    if (rowsAffected == 0)
                    {
                        _logger.LogInformation($"Message {message.Id} was already claimed or is actively processing by another worker.");
                        continue;
                    }
                }

                // Status and LastAttemptAt were updated in DB, now update local tracking entity
                message.Status = "PROCESSING";
                message.LastAttemptAt = now;

                try
                {
                    await notificationEngine.ProcessOutboxMessageAsync(message, dbContext);
                    
                    message.Status = "PROCESSED";
                    message.LastError = null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to process outbox message {message.Id}");
                    message.RetryCount++;
                    message.LastError = ex.Message;
                    
                    if (message.RetryCount >= MaxRetries)
                    {
                        message.Status = "FAILED";
                    }
                    else
                    {
                        message.Status = "PENDING"; // Revert to PENDING for retry
                    }
                }
                finally
                {
                    // Save changes per message to avoid one bad message rolling back the others
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }
        }
    }
}
