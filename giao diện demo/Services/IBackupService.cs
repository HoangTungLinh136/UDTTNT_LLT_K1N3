using System.Threading.Tasks;
using System.IO;

namespace giao_dien_demo.Services
{
    public interface IBackupService
    {
        Task<long> CreateBackupAsync(string type, string userId);
        Task RestoreBackupAsync(long backupId, string userId);
        Task<bool> VerifyBackupAsync(long backupId);
        Task DeleteBackupAsync(long backupId, string userId);
        Task<Stream> DownloadBackupAsync(long backupId, string userId);
    }
}
