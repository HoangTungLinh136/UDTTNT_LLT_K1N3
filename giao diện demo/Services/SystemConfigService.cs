using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using giao_dien_demo.Data;
using giao_dien_demo.Models;

namespace giao_dien_demo.Services
{
    public interface ISystemConfigService
    {
        Task<Dictionary<string, string>> GetAllConfigsAsync();
        Task<string> GetValueAsync(string key, string defaultValue = null);
        Task UpdateConfigsAsync(Dictionary<string, string> configs, string updatedBy);
        Task UpdateValueAsync(string key, string value, string updatedBy);
    }

    public class SystemConfigService : ISystemConfigService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IAuditLogService _auditLogService;
        private const string CacheKey = "SystemConfigs_Cache";

        public SystemConfigService(ApplicationDbContext context, IMemoryCache cache, IAuditLogService auditLogService)
        {
            _context = context;
            _cache = cache;
            _auditLogService = auditLogService;
        }

        public async Task<Dictionary<string, string>> GetAllConfigsAsync()
        {
            if (!_cache.TryGetValue(CacheKey, out Dictionary<string, string> configs))
            {
                var dbConfigs = await _context.SystemConfigs.ToListAsync();
                configs = dbConfigs.ToDictionary(c => c.Key, c => c.Value);
                
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromHours(24));
                _cache.Set(CacheKey, configs, cacheEntryOptions);
            }
            return configs;
        }

        public async Task<string> GetValueAsync(string key, string defaultValue = null)
        {
            var configs = await GetAllConfigsAsync();
            if (configs.TryGetValue(key, out string value))
            {
                return value;
            }
            return defaultValue;
        }

        public async Task UpdateConfigsAsync(Dictionary<string, string> updates, string updatedBy)
        {
            var keys = updates.Keys.ToList();
            var existingConfigs = await _context.SystemConfigs.Where(c => keys.Contains(c.Key)).ToListAsync();

            foreach (var kvp in updates)
            {
                var config = existingConfigs.FirstOrDefault(c => c.Key == kvp.Key);
                if (config != null)
                {
                    if (config.Value != kvp.Value)
                    {
                        var oldVal = config.Value;
                        config.Value = kvp.Value;
                        config.UpdatedAt = DateTime.Now;
                        config.UpdatedBy = updatedBy;
                        _context.Update(config);

                        // Audit log
                        await _auditLogService.LogAsync("UpdateConfig", "SystemConfiguration", "SystemConfig", config.Key, "System", "Success", null, oldVal, kvp.Value, null);
                    }
                }
            }

            await _context.SaveChangesAsync();
            _cache.Remove(CacheKey);
        }

        public async Task UpdateValueAsync(string key, string value, string updatedBy)
        {
            var config = await _context.SystemConfigs.FirstOrDefaultAsync(c => c.Key == key);
            if (config != null && config.Value != value)
            {
                var oldVal = config.Value;
                config.Value = value;
                config.UpdatedAt = DateTime.Now;
                config.UpdatedBy = updatedBy;

                _context.Update(config);
                await _context.SaveChangesAsync();
                _cache.Remove(CacheKey);

                await _auditLogService.LogAsync("UpdateConfig", "SystemConfiguration", "SystemConfig", key, "System", "Success", null, oldVal, value, null);
            }
        }
    }
}
