using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace giao_dien_demo.Services
{
    public class SecuritySettings
    {
        // Password Policy
        public int MinPasswordLength { get; set; } = 6;
        public bool RequireUppercase { get; set; } = false;
        public bool RequireLowercase { get; set; } = false;
        public bool RequireDigit { get; set; } = false;
        public bool RequireSpecialChar { get; set; } = false;

        // Session Policy
        public int SessionIdleTimeoutMinutes { get; set; } = 30;

        // Login Policy
        public List<string> TrustedIPs { get; set; } = new List<string>();
        public bool EnableIPWhitelist { get; set; } = false;
    }

    public static class SecuritySettingsService
    {
        private static readonly string filePath = Path.Combine(Directory.GetCurrentDirectory(), "SecuritySettings.json");
        private static readonly object _lock = new object();

        public static SecuritySettings GetSettings()
        {
            lock (_lock)
            {
                if (!File.Exists(filePath))
                {
                    var defaultSettings = new SecuritySettings();
                    SaveSettings(defaultSettings);
                    return defaultSettings;
                }

                try
                {
                    string json = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<SecuritySettings>(json) ?? new SecuritySettings();
                }
                catch
                {
                    return new SecuritySettings();
                }
            }
        }

        public static void SaveSettings(SecuritySettings settings)
        {
            lock (_lock)
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
        }

        // Validate Password based on policy
        public static string ValidatePassword(string password)
        {
            var settings = GetSettings();
            if (string.IsNullOrEmpty(password) || password.Length < settings.MinPasswordLength)
                return $"Mật khẩu phải dài tối thiểu {settings.MinPasswordLength} ký tự.";
            if (settings.RequireUppercase && !System.Text.RegularExpressions.Regex.IsMatch(password, "[A-Z]"))
                return "Mật khẩu phải chứa ít nhất 1 chữ viết hoa.";
            if (settings.RequireLowercase && !System.Text.RegularExpressions.Regex.IsMatch(password, "[a-z]"))
                return "Mật khẩu phải chứa ít nhất 1 chữ viết thường.";
            if (settings.RequireDigit && !System.Text.RegularExpressions.Regex.IsMatch(password, "[0-9]"))
                return "Mật khẩu phải chứa ít nhất 1 chữ số.";
            if (settings.RequireSpecialChar && !System.Text.RegularExpressions.Regex.IsMatch(password, "[^a-zA-Z0-9]"))
                return "Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt.";
            return null; // Valid
        }
    }
}
