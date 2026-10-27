using System.Collections.Generic;
using giao_dien_demo.Services;

namespace giao_dien_demo.Models
{
    public class AdminIntegrationViewModel
    {
        public NotificationSettings EmailConfig { get; set; } = new NotificationSettings();
        public List<CameraConfig> CameraConfigs { get; set; } = new List<CameraConfig>();
        public AiProviderSettings AiConfig { get; set; } = new AiProviderSettings();
    }
}
