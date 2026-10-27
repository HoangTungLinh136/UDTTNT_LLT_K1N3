using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;

namespace giao_dien_demo.Services
{
    public class BackupService : IBackupService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;
        private readonly IAuditLogService _auditLogService;
        private readonly string _backupDirectory;

        private static readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);

        public BackupService(ApplicationDbContext context, IConfiguration configuration, IWebHostEnvironment env, IAuditLogService auditLogService)
        {
            _context = context;
            _configuration = configuration;
            _env = env;
            _auditLogService = auditLogService;
            
            _backupDirectory = Path.Combine(_env.ContentRootPath, "App_Data", "Backups");
            if (!Directory.Exists(_backupDirectory))
            {
                Directory.CreateDirectory(_backupDirectory);
            }
        }

        private string ComputeSha256(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        public async Task<long> CreateBackupAsync(string type, string userId)
        {
            if (!await _operationLock.WaitAsync(TimeSpan.FromSeconds(0)))
            {
                throw new Exception("Một tiến trình Sao lưu hoặc Phục hồi khác đang chạy. Vui lòng thử lại sau.");
            }

            try
            {
                if (type != "Full") type = "Full"; // Only Full is supported for now via simple ADO
            
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            var builder = new SqlConnectionStringBuilder(connectionString);
            var databaseName = builder.InitialCatalog;

            var fileName = $"HRMS_{databaseName}_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
            var filePath = Path.Combine(_backupDirectory, fileName);

            var record = new BackupRecord
            {
                FileName = fileName,
                FilePath = filePath,
                Type = type,
                Status = "InProgress",
                CreatedAt = DateTime.Now,
                CreatedBy = userId,
                IsDeleted = false
            };
            
            _context.BackupRecords.Add(record);
            await _context.SaveChangesAsync();

            try
            {
                var query = $"BACKUP DATABASE [{databaseName}] TO DISK = '{filePath}' WITH INIT";
                await _context.Database.ExecuteSqlRawAsync(query);

                var fileInfo = new FileInfo(filePath);
                record.SizeBytes = fileInfo.Length;
                record.Checksum = ComputeSha256(filePath);
                record.Status = "Success";
                record.CompletedAt = DateTime.Now;

                await _auditLogService.LogAsync("CreateBackup", "BackupService", "BackupRecord", record.Id.ToString(), "Security", "Success", $"Backup created successfully: {fileName}");
            }
            catch (Exception ex)
            {
                record.Status = "Failed";
                record.FailureReason = ex.Message;
                record.CompletedAt = DateTime.Now;

                await _auditLogService.LogAsync("CreateBackup", "BackupService", "BackupRecord", record.Id.ToString(), "Security", "Failed", $"Backup failed: {ex.Message}");
                throw;
            }
            finally
            {
                _context.Update(record);
                await _context.SaveChangesAsync();
            }

            return record.Id;
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public async Task RestoreBackupAsync(long backupId, string userId)
        {
            if (!await _operationLock.WaitAsync(TimeSpan.FromSeconds(0)))
            {
                throw new Exception("Một tiến trình Sao lưu hoặc Phục hồi khác đang chạy. Vui lòng thử lại sau.");
            }

            try
            {
                var record = await _context.BackupRecords.FindAsync(backupId);
                if (record == null || record.IsDeleted || record.Status != "Success")
                    throw new Exception("Bản sao lưu không hợp lệ hoặc đã bị xoá.");

            if (!File.Exists(record.FilePath))
            {
                throw new Exception("File sao lưu không còn tồn tại trên hệ thống lưu trữ.");
            }

            // Verify checksum before restore
            var currentChecksum = ComputeSha256(record.FilePath);
            if (currentChecksum != record.Checksum)
            {
                throw new Exception("CẢNH BÁO: File sao lưu đã bị thay đổi (Checksum không khớp). Không thể phục hồi!");
            }

            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            var builder = new SqlConnectionStringBuilder(connectionString);
            var databaseName = builder.InitialCatalog;

            // Connect to master to restore the target database
            builder.InitialCatalog = "master";
            var masterConnectionString = builder.ConnectionString;

            using (var connection = new SqlConnection(masterConnectionString))
            {
                await connection.OpenAsync();
                
                try
                {
                    // 1. Kick out other users
                    var singleUserQuery = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE";
                    using (var cmd = new SqlCommand(singleUserQuery, connection))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 2. Restore Database
                    var restoreQuery = $"RESTORE DATABASE [{databaseName}] FROM DISK = '{record.FilePath}' WITH REPLACE";
                    using (var cmd = new SqlCommand(restoreQuery, connection))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                finally
                {
                    // 3. Set back to Multi User
                    var multiUserQuery = $"ALTER DATABASE [{databaseName}] SET MULTI_USER";
                    using (var cmd = new SqlCommand(multiUserQuery, connection))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }

            // Ghi nhận Audit
            await _auditLogService.LogAsync("RestoreBackup", "BackupService", "BackupRecord", backupId.ToString(), "Security", "Success", $"Restored from backup: {record.FileName}");
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public async Task<bool> VerifyBackupAsync(long backupId)
        {
            var record = await _context.BackupRecords.FindAsync(backupId);
            if (record == null || record.IsDeleted || !File.Exists(record.FilePath))
                return false;

            var currentChecksum = ComputeSha256(record.FilePath);
            return currentChecksum == record.Checksum;
        }

        public async Task DeleteBackupAsync(long backupId, string userId)
        {
            var record = await _context.BackupRecords.FindAsync(backupId);
            if (record == null || record.IsDeleted) return;

            if (File.Exists(record.FilePath))
            {
                File.Delete(record.FilePath);
            }

            record.IsDeleted = true;
            _context.Update(record);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync("DeleteBackup", "BackupService", "BackupRecord", backupId.ToString(), "Security", "Success", $"Deleted backup: {record.FileName}");
        }

        public async Task<Stream> DownloadBackupAsync(long backupId, string userId)
        {
            var record = await _context.BackupRecords.FindAsync(backupId);
            if (record == null || record.IsDeleted || !File.Exists(record.FilePath))
                throw new FileNotFoundException("Không tìm thấy file backup.");

            await _auditLogService.LogAsync("DownloadBackup", "BackupService", "BackupRecord", backupId.ToString(), "Security", "Success", $"Downloaded backup: {record.FileName}");
            
            return new FileStream(record.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }
}
