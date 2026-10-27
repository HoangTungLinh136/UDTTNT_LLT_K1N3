using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using giao_dien_demo.Services;

namespace giao_dien_demo.Middlewares
{
    public class MaintenanceMiddleware
    {
        private readonly RequestDelegate _next;

        public MaintenanceMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ISystemConfigService systemConfigService)
        {
            // Kiểm tra trạng thái bảo trì
            string isMaintenanceMode = await systemConfigService.GetValueAsync("Sys.MaintenanceMode", "false");

            if (isMaintenanceMode == "true")
            {
                var role = context.Session.GetString("Role");
                var path = context.Request.Path.Value?.ToLower();

                // Nếu không phải Admin và không ở trang Login/Account -> Chặn
                if (role != "Admin" && path != null && !path.StartsWith("/account"))
                {
                    // Chuyển hướng sang trang báo đang bảo trì hoặc trả về lỗi
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.WriteAsync(@"
                        <!DOCTYPE html>
                        <html>
                        <head><title>Hệ thống đang bảo trì</title></head>
                        <body style='font-family:sans-serif; text-align:center; padding-top:50px; background:#f8fafc;'>
                            <h1 style='color:#dc2626;'>Hệ thống đang được bảo trì</h1>
                            <p>Chúng tôi đang thực hiện nâng cấp hệ thống. Vui lòng quay lại sau.</p>
                            <p><a href='/Account/Login' style='color:#2563eb;'>Đăng nhập Admin</a></p>
                        </body>
                        </html>
                    ");
                    return;
                }
            }

            await _next(context);
        }
    }
}
