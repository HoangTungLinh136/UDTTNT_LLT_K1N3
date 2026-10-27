using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace giao_dien_demo.Services
{
    public class AiProviderSettings
    {
        public string ProviderName { get; set; } = "Groq";
        public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1/";
        public string ModelName { get; set; } = "llama-3.1-8b-instant";
        public string ApiKey { get; set; } = "";
        public bool IsActive { get; set; } = true;
    }

    public class IntegrationSettings
    {
        public AiProviderSettings AiProvider { get; set; } = new AiProviderSettings();
    }

    public class IntegrationConfigService
    {
        private readonly string _filePath = "integration_settings.json";
        private readonly IConfiguration _configuration;

        public IntegrationConfigService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IntegrationSettings LoadSettings()
        {
            if (!File.Exists(_filePath))
            {
                var defaultSettings = new IntegrationSettings();
                SaveSettings(defaultSettings);
                ApplyExternalSecrets(defaultSettings);
                return defaultSettings;
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                var settings = JsonSerializer.Deserialize<IntegrationSettings>(json) ?? new IntegrationSettings();
                ApplyExternalSecrets(settings);
                return settings;
            }
            catch
            {
                var settings = new IntegrationSettings();
                ApplyExternalSecrets(settings);
                return settings;
            }
        }

        public void SaveSettings(IntegrationSettings settings)
        {
            // Provider credentials are supplied only by external .NET configuration
            // (for example Integrations__AI__ApiKey or user-secrets), never this file.
            settings.AiProvider.ApiKey = string.Empty;
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }

        private void ApplyExternalSecrets(IntegrationSettings settings)
        {
            settings.AiProvider.ApiKey = _configuration["Integrations:AI:ApiKey"] ?? string.Empty;
        }
    }
}
