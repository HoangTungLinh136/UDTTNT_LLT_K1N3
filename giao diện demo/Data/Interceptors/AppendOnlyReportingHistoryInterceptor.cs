using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using giao_dien_demo.Models;

namespace giao_dien_demo.Data.Interceptors
{
    public class AppendOnlyReportingHistoryInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            EnforceAppendOnly(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, 
            InterceptionResult<int> result, 
            CancellationToken cancellationToken = default)
        {
            EnforceAppendOnly(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static void EnforceAppendOnly(DbContext? context)
        {
            if (context == null) return;

            var modifiedOrDeletedEntries = context.ChangeTracker.Entries<EmployeeReportingHistory>()
                .Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted)
                .ToList();

            if (modifiedOrDeletedEntries.Any())
            {
                throw new InvalidOperationException(
                    "VI PHẠM BẢO MẬT (OD-09): Thực thể EmployeeReportingHistory là dữ liệu append-only bất biến. " +
                    "Tuyệt đối nghiêm cấm các thao tác Cập nhật (UPDATE) hoặc Xóa (DELETE) qua Entity Framework!");
            }
        }
    }
}

