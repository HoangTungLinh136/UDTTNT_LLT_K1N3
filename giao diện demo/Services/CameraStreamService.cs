using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using giao_dien_demo.Data;
using Microsoft.EntityFrameworkCore;

namespace giao_dien_demo.Services
{
    public class CameraStreamService : BackgroundService
    {
        private readonly ILogger<CameraStreamService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private Process? _ffmpegProcess;
        private string _currentRtspUrl = string.Empty;

        public CameraStreamService(ILogger<CameraStreamService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var protector = scope.ServiceProvider.GetRequiredService<giao_dien_demo.Services.ICameraCredentialProtector>();
                    var config = await dbContext.CameraConfigs.OrderBy(c => c.Id).FirstOrDefaultAsync(stoppingToken);

                    if (config != null && !string.IsNullOrEmpty(config.RtspUrl) && config.IsActive)
                    {
                        var plainRtsp = protector.Unprotect(config.RtspUrl);

                        if (_currentRtspUrl != config.RtspUrl || _ffmpegProcess == null || _ffmpegProcess.HasExited)
                        {
                            StopFFmpeg();
                            _currentRtspUrl = config.RtspUrl;
                            if (!string.IsNullOrEmpty(plainRtsp))
                            {
                                StartFFmpegHls(plainRtsp);
                            }
                        }
                    }
                    else
                    {
                        StopFFmpeg();
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        if (!stoppingToken.IsCancellationRequested)
                        {
                            _logger.LogError(ex, "Error in CameraStreamService");
                        }
                    }
                    catch { } // Prevent host crash if logger is already disposed
                }

                try 
                {
                    // Check configuration every 10 seconds
                    try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); } catch (System.OperationCanceledException) { break; }
                }
                catch (TaskCanceledException) 
                {
                    break;
                }
            }

            StopFFmpeg();
        }

        private void StartFFmpegHls(string rtspUrl)
        {
            string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "hls");
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Using FFmpeg to create an HLS stream (m3u8 + ts files)
            string arguments = $"-rtsp_transport tcp -i \"{rtspUrl}\" -fflags nobuffer -c:v libx264 -preset veryfast -tune zerolatency -f hls -hls_time 2 -hls_list_size 5 -hls_flags delete_segments \"{Path.Combine(outputDir, "stream.m3u8")}\"";

            string safeArgs = arguments.Replace(rtspUrl, "rtsp://***:***@...");
            _logger.LogInformation("Starting FFmpeg: ffmpeg " + safeArgs);

            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                _ffmpegProcess = Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start FFmpeg. Ensure ffmpeg is installed and in PATH.");
            }
        }

        private void StopFFmpeg()
        {
            if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
            {
                try
                {
                    _ffmpegProcess.Kill();
                    _ffmpegProcess.Dispose();
                }
                catch { }
                _ffmpegProcess = null;
                _currentRtspUrl = string.Empty;
                _logger.LogInformation("Stopped FFmpeg stream.");
            }
        }
    }
}

