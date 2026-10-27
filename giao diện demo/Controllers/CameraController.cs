using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using giao_dien_demo.Authorization;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using Microsoft.EntityFrameworkCore;

namespace giao_dien_demo.Controllers
{
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.HR},{AppRoles.Leader}")]
    [AutoValidateAntiforgeryToken]
    public class CameraController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly giao_dien_demo.Services.ICameraCredentialProtector _protector;

        public CameraController(ApplicationDbContext context, giao_dien_demo.Services.ICameraCredentialProtector protector)
        {
            _context = context;
            _protector = protector;
        }

        // GET: Camera/Config
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> Config()
        {
            var config = await _context.CameraConfigs.AsNoTracking().FirstOrDefaultAsync() ?? new CameraConfig();
            config.Password = "";
            
            var plainRtsp = _protector.Unprotect(config.RtspUrl);
            if (!string.IsNullOrEmpty(plainRtsp))
            {
                config.RtspUrl = System.Text.RegularExpressions.Regex.Replace(plainRtsp, @"rtsp://[^@]+@", "rtsp://***:***@");
            }
            return View(config);
        }

        // POST: Camera/Config
        [HttpPost]
        [Authorize(Roles = AppRoles.Admin)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Config(CameraConfig config)
        {
            if (ModelState.IsValid)
            {
                var existingConfig = await _context.CameraConfigs.FirstOrDefaultAsync();
                if (existingConfig != null)
                {
                    existingConfig.Name = config.Name;
                    existingConfig.IPAddress = config.IPAddress;
                    if (!string.IsNullOrEmpty(config.RtspUrl) && config.RtspUrl.Contains("***:***@"))
                    {
                        // Unchanged. Migrate if legacy.
                        if (_protector.IsProtected(existingConfig.RtspUrl))
                        {
                            config.RtspUrl = existingConfig.RtspUrl;
                        }
                        else
                        {
                            config.RtspUrl = _protector.Protect(existingConfig.RtspUrl);
                        }
                    }
                    else
                    {
                        // Protect new URL
                        existingConfig.RtspUrl = _protector.Protect(config.RtspUrl);
                    }
                    
                    existingConfig.Username = config.Username;
                    if (!string.IsNullOrEmpty(config.Password))
                    {
                        existingConfig.Password = _protector.Protect(config.Password);
                    }
                    else if (!_protector.IsProtected(existingConfig.Password) && !string.IsNullOrEmpty(existingConfig.Password))
                    {
                        // Migrate legacy plaintext password
                        existingConfig.Password = _protector.Protect(existingConfig.Password);
                    }
                    _context.Update(existingConfig);
                }
                else
                {
                    _context.Add(config);
                }
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Camera configuration saved successfully.";
                return RedirectToAction(nameof(Live));
            }
            return View(config);
        }

        // GET: Camera/Live
        public async Task<IActionResult> Live()
        {
            var config = await _context.CameraConfigs.FirstOrDefaultAsync();
            if (config == null || string.IsNullOrEmpty(config.RtspUrl))
            {
                TempData["ErrorMessage"] = "Please configure your camera first.";
                return RedirectToAction(nameof(Config));
            }
            return View(config);
        }

        // GET: Camera/History
        public async Task<IActionResult> History()
        {
            var events = await _context.CameraEvents
                .Include(e => e.CameraConfig)
                .OrderByDescending(e => e.Timestamp)
                .Take(100)
                .ToListAsync();
            return View(events);
        }
    }
}
