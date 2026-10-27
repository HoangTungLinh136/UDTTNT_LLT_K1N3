using System;
using System.IO;
using System.Text.Json;

namespace giao_dien_demo.Services
{
    public class SmtpSettings
    {
        public string Host { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public bool EnableSsl { get; set; } = true;
        public string SenderName { get; set; } = "HRMS System";
        public string SenderEmail { get; set; } = "";
    }

    public class NotificationSettings
    {
        public SmtpSettings Smtp { get; set; } = new SmtpSettings();
    }

    public class NotificationConfigService
    {
        private readonly string _filePath = "notification_settings.json";

        public NotificationSettings LoadSettings()
        {
            if (!File.Exists(_filePath))
            {
                var defaultSettings = new NotificationSettings();
                SaveSettings(defaultSettings);
                return defaultSettings;
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<NotificationSettings>(json) ?? new NotificationSettings();
            }
            catch
            {
                return new NotificationSettings();
            }
        }

        public void SaveSettings(NotificationSettings settings)
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
    }
}
