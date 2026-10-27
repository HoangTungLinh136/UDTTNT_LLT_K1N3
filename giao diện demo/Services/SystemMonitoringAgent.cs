using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using giao_dien_demo.Data;
using giao_dien_demo.Models;

namespace giao_dien_demo.Services
{
    public class SystemMonitoringAgent : BackgroundService
    {
        private readonly ITelemetryStore _telemetryStore;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SystemMonitoringAgent> _logger;
        private int _cpuHighCount = 0;

        private TimeSpan _lastProcessorTime;
        private DateTime _lastCheckTime;

        public SystemMonitoringAgent(ITelemetryStore telemetryStore, IServiceScopeFactory scopeFactory, ILogger<SystemMonitoringAgent> logger)
        {
            _telemetryStore = telemetryStore;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SystemMonitoringAgent is starting.");

            var process = Process.GetCurrentProcess();
            _lastProcessorTime = process.TotalProcessorTime;
            _lastCheckTime = DateTime.UtcNow;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CollectAndEvaluateMetricsAsync();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in SystemMonitoringAgent loop");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task CollectAndEvaluateMetricsAsync()
        {
            // 1. Thu thập OS Metrics
            var process = Process.GetCurrentProcess();
            
            var currentCpuTime = process.TotalProcessorTime;
            var currentTime = DateTime.UtcNow;

            var cpuUsedMs = (currentCpuTime - _lastProcessorTime).TotalMilliseconds;
            var totalMsPassed = (currentTime - _lastCheckTime).TotalMilliseconds;
            
            double cpuUsage = 0;
            if (totalMsPassed > 0)
            {
                cpuUsage = (cpuUsedMs / (Environment.ProcessorCount * totalMsPassed)) * 100;
            }
            if (cpuUsage < 0) cpuUsage = 0;
            if (cpuUsage > 100) cpuUsage = 100;

            _lastProcessorTime = currentCpuTime;
            _lastCheckTime = currentTime;

            double ramMb = process.WorkingSet64 / 1024.0 / 1024.0;
            
            double diskPercent = 0;
            try
            {
                var drive = new DriveInfo(Directory.GetDirectoryRoot(Directory.GetCurrentDirectory()));
                diskPercent = 100.0 * (drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize;
            }
            catch { }

            var point = new ServerMetricPoint
            {
                Timestamp = DateTime.Now,
                CpuUsagePercent = cpuUsage,
                RamUsageMb = ramMb,
                DiskUsagePercent = diskPercent,
                NetworkInKbps = 0, // Cross-platform network throughput is unavailable natively without elevated counters
                NetworkOutKbps = 0
            };

            _telemetryStore.AddServerMetric(point);

            // 2. Alert Engine
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Nếu CPU > 90%
                if (cpuUsage > 90)
                {
                    _cpuHighCount++;
                    if (_cpuHighCount >= 3) // 3 nhịp liên tiếp
                    {
                        var activeAlert = dbContext.MonitoringAlerts.FirstOrDefault(a => a.MetricName == "CPU" && a.Status != "Resolved");
                        if (activeAlert == null)
                        {
                            dbContext.MonitoringAlerts.Add(new MonitoringAlert
                            {
                                AlertName = "CPU Usage High",
                                MetricName = "CPU",
                                Severity = "High",
                                CurrentValue = $"{cpuUsage:F1}%",
                                Threshold = "> 90%",
                                StartedAt = DateTime.Now,
                                Status = "Active",
                                AcknowledgedBy = ""
                            });
                            await dbContext.SaveChangesAsync();
                        }
                        else
                        {
                            activeAlert.CurrentValue = $"{cpuUsage:F1}%";
                            await dbContext.SaveChangesAsync();
                        }
                    }
                }
                else
                {
                    _cpuHighCount = 0;
                    // Auto-resolve CPU alerts
                    var activeAlerts = dbContext.MonitoringAlerts.Where(a => a.MetricName == "CPU" && a.Status != "Resolved").ToList();
                    if (activeAlerts.Any())
                    {
                        foreach (var alert in activeAlerts)
                        {
                            alert.Status = "Resolved";
                            alert.ResolvedAt = DateTime.Now;
                        }
                        await dbContext.SaveChangesAsync();
                    }
                }
            }
        }
    }
}
