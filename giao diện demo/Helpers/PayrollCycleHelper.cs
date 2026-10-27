using System;

namespace giao_dien_demo.Helpers
{
    public class PayrollCycle
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string PeriodName { get; set; } = string.Empty;
        public DateTime PaymentDate { get; set; }
    }

    public static class PayrollCycleHelper
    {
        /// <summary>
        /// Xác định đợt lương dựa trên ngày hiện tại.
        /// Đợt lương: từ 00:00:00 ngày 11 tháng trước đến 23:59:59 ngày 10 tháng hiện tại.
        /// Sang 00:01 ngày 11 sẽ tự động nhảy sang đợt mới.
        /// </summary>
        public static PayrollCycle GetCurrentCycle(DateTime currentDate)
        {
            DateTime startDate;
            DateTime endDate;
            DateTime paymentDate;
            string periodName;

            // Nếu ngày hiện tại < 11: Đang thuộc chu kỳ thanh toán cho tháng hiện tại (chu kỳ tính công tháng trước)
            // Ví dụ: Hôm nay là 05/08 -> Chu kỳ là 11/07 - 10/08. Tên kỳ: T08/yyyy
            if (currentDate.Day < 11)
            {
                startDate = new DateTime(currentDate.AddMonths(-1).Year, currentDate.AddMonths(-1).Month, 11, 0, 0, 0);
                endDate = new DateTime(currentDate.Year, currentDate.Month, 10, 23, 59, 59);
                paymentDate = new DateTime(currentDate.Year, currentDate.Month, 10, 0, 0, 0);
                periodName = $"T{currentDate.Month:D2}/{currentDate.Year}";
            }
            // Nếu ngày hiện tại >= 11: Đang thuộc chu kỳ thanh toán cho tháng sau (chu kỳ tính công tháng này)
            // Ví dụ: Hôm nay là 15/08 -> Chu kỳ là 11/08 - 10/09. Tên kỳ: T09/yyyy
            else
            {
                startDate = new DateTime(currentDate.Year, currentDate.Month, 11, 0, 0, 0);
                var nextMonth = currentDate.AddMonths(1);
                endDate = new DateTime(nextMonth.Year, nextMonth.Month, 10, 23, 59, 59);
                paymentDate = new DateTime(nextMonth.Year, nextMonth.Month, 10, 0, 0, 0);
                periodName = $"T{nextMonth.Month:D2}/{nextMonth.Year}";
            }

            return new PayrollCycle
            {
                StartDate = startDate,
                EndDate = endDate,
                PaymentDate = paymentDate,
                PeriodName = periodName
            };
        }

        public static PayrollCycle GetCycleByPeriod(string periodName)
        {
            // periodName format: T08/2026
            if (string.IsNullOrEmpty(periodName) || periodName.Length < 7)
                return GetCurrentCycle(DateTime.Now);

            try
            {
                int month = int.Parse(periodName.Substring(1, 2));
                int year = int.Parse(periodName.Substring(4, 4));
                // We construct a date that falls in this cycle.
                // The cycle for T08/2026 ends on 10/08/2026.
                // So any date <= 10th of the month will belong to this cycle.
                DateTime dateInCycle = new DateTime(year, month, 10);
                return GetCurrentCycle(dateInCycle);
            }
            catch
            {
                return GetCurrentCycle(DateTime.Now);
            }
        }
    }
}
