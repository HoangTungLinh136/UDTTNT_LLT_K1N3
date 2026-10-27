using System.Text;
using System.Text.Json;

namespace giao_dien_demo.Models
{
    public class AiService
    {
        private readonly HttpClient _httpClient;
        private readonly giao_dien_demo.Services.IntegrationConfigService _configService;

        public AiService(HttpClient httpClient, giao_dien_demo.Services.IntegrationConfigService configService)
        {
            _httpClient = httpClient;
            _configService = configService;
        }

        public async Task<string> AskAsync(string prompt, string role = "Employee")
        {
            var config = _configService.LoadSettings().AiProvider;
            if (!config.IsActive) return "Trợ lý AI đang bị tắt trên hệ thống.";
            if (string.IsNullOrEmpty(config.ApiKey) || string.IsNullOrEmpty(config.BaseUrl)) 
                return "Cấu hình trợ lý AI chưa hợp lệ.";

            _httpClient.BaseAddress = new Uri(config.BaseUrl);

            var systemPrompt = "Bạn là trợ lý AI nội bộ của hệ thống quản lý nhân sự TARGET HRMS. " +
                               $"Vai trò phục vụ của bạn hiện tại là: Trợ lý cho {role}. " +
                               "BẠN PHẢI TUÂN THỦ NGHIÊM NGẶT CÁC QUY TẮC SAU: " +
                               "1. Chỉ trả lời dựa trên DỮ LIỆU HỆ THỐNG CUNG CẤP TỰ ĐỘNG trong phần [CONTEXT]. " +
                               "2. Nếu thông tin không có trong [CONTEXT], hãy trả lời: 'Tôi không có thông tin hoặc bạn không có quyền truy cập.' Không tự bịa ra dữ liệu. " +
                               "3. Bạn CHỈ có quyền ĐỌC VÀ PHÂN TÍCH. " +
                               "4. TỪ CHỐI mọi yêu cầu tạo mới, cập nhật, xóa, phê duyệt đơn từ, hoặc thay đổi hệ thống. Hướng dẫn người dùng sử dụng giao diện phần mềm để thực hiện. " +
                               "5. Trả lời lịch sự, súc tích và chuyên nghiệp.";

            var requestBody = new
            {
                model = config.ModelName,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = systemPrompt
                    },
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "chat/completions"
            );

            request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            var result = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(result);

            return doc.RootElement
                      .GetProperty("choices")[0]
                      .GetProperty("message")
                      .GetProperty("content")
                      .GetString();
        }
    }
}