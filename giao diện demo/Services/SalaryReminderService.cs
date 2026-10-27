using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Helpers;

namespace giao_dien_demo.Services
{
    public class SalaryReminderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public SalaryReminderService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Chờ app khởi động xong trước khi chạy lần đầu
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndSendReminder(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // App đang shutdown — thoát vòng lặp
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // IServiceProvider đã bị disposed — thoát vòng lặp
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SalaryReminderService Error: {ex.Message}");
                }

                try
                {
                    // Chờ 10 phút trước khi kiểm tra lại
                    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // App đang shutdown — thoát vòng lặp bình thường
                    break;
                }
            }
        }


        private async Task CheckAndSendReminder(CancellationToken cancellationToken = default)
        {
            // Kiểm tra nếu đang shutdown
            if (cancellationToken.IsCancellationRequested) return;

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var notifications = scope.ServiceProvider.GetRequiredService<IInAppNotificationService>();
                
                // 1. Lấy thời gian hiện tại theo Việt Nam
                TimeZoneInfo vnTimeZone;
                try
                {
                    vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                }
                catch (TimeZoneNotFoundException)
                {
                    vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
                }
                var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);

                Console.WriteLine($"[SalaryReminder] Current Vietnam time: {now:yyyy-MM-dd HH:mm}");

                // 2. Tìm tất cả các kỳ lương có khoản thanh toán chưa hoàn thành
                var unpaidPeriods = dbContext.PayrollPayments
                    .Where(p => p.Status == PaymentStatus.ChoThanhToan || p.Status == PaymentStatus.ChuaThanhToan)
                    .Select(p => p.PayrollPeriod)
                    .Distinct()
                    .ToList();

                // Kiểm tra trường hợp HR quên chưa tạo danh sách thanh toán cho kỳ trước
                var previousCycle = PayrollCycleHelper.GetCurrentCycle(now.AddMonths(-1));
                if (now > previousCycle.EndDate)
                {
                    bool isMissing = !dbContext.PayrollPayments.Any(p => p.PayrollPeriod == previousCycle.PeriodName);
                    bool hasEmployees = dbContext.Employees.Any();
                    if (isMissing && hasEmployees && !unpaidPeriods.Contains(previousCycle.PeriodName))
                    {
                        unpaidPeriods.Add(previousCycle.PeriodName);
                        Console.WriteLine($"[SalaryReminder] Period {previousCycle.PeriodName} is completely missing (not generated). Added to check list.");
                    }
                }

                if (!unpaidPeriods.Any())
                {
                    Console.WriteLine("[SalaryReminder] No pending payroll payment. No reminder required.");
                    return;
                }

                // Lấy danh sách HR có quyền xử lý
                var hrs = dbContext.Users.Where(u => u.Role == "HR").ToList();
                if (!hrs.Any()) return;

                foreach (var period in unpaidPeriods)
                {
                    if (string.IsNullOrEmpty(period)) continue;

                    // Xác định chu kỳ của đợt lương này
                    var cycle = PayrollCycleHelper.GetCycleByPeriod(period);
                    
                    Console.WriteLine($"[SalaryReminder] Checking payroll period: {cycle.StartDate:yyyy-MM-dd} -> {cycle.EndDate:yyyy-MM-dd} ({period})");

                    // 3. Nếu đã qua ngày kết thúc của chu kỳ (tức là sau 23:59:59 ngày 10)
                    if (now > cycle.EndDate)
                    {
                        var pendingCount = dbContext.PayrollPayments.Count(p => p.PayrollPeriod == period && (p.Status == PaymentStatus.ChoThanhToan || p.Status == PaymentStatus.ChuaThanhToan));
                        Console.WriteLine($"[SalaryReminder] Pending payments: {pendingCount}");
                        Console.WriteLine($"[SalaryReminder] HR recipients: {hrs.Count}");

                        foreach (var hr in hrs)
                        {
                            var username = hr.Username;

                            string msgDetail = pendingCount > 0
                                ? $"Vẫn còn {pendingCount} khoản chưa được xác nhận thanh toán."
                                : "Chưa có danh sách thanh toán nào được tạo.";
                            var message = $"⚠️ Bạn chưa xác nhận chuyển lương cho đợt {period} từ ngày {cycle.StartDate:dd/MM/yyyy} đến ngày {cycle.EndDate:dd/MM/yyyy}. {msgDetail} Vui lòng kiểm tra và xử lý.";
                            var created = await notifications.SendAsync(
                                username, message, "SALARY_PAYMENT_REMINDER",
                                deduplicationKey: $"salary-reminder:{period}");

                            if (created.Count > 0)
                            {
                                Console.WriteLine($"[SalaryReminder] Reminder created for HR UserId: {hr.Id}, Username: {hr.Username}");
                            }
                            else
                            {
                                Console.WriteLine($"[SalaryReminder] Reminder already exists for {username}. Skip.");
                            }
                        }
                    }
                }
            }
        }
    }
}
