using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace giao_dien_demo.Services
{
    public interface IProtectedFileService
    {
        Task<(string CanonicalId, string FilePath)> SaveProtectedFileAsync(string module, string parentId, IFormFile file);
        Task<(string CanonicalId, string FilePath)> SaveProtectedFileFromBytesAsync(string module, string parentId, byte[] fileBytes, string extension);
        Task<(string PhysicalPath, string ContentType, string OriginalName)> GetProtectedFileAsync(string canonicalId);
    }

    public class ProtectedFileService : IProtectedFileService
    {
        private readonly string _baseStoragePath;
        private readonly ILogger<ProtectedFileService> _logger;

        public ProtectedFileService(IConfiguration configuration, ILogger<ProtectedFileService> logger)
        {
            _logger = logger;
            // Default to App_Data/ProtectedFiles if not configured
            _baseStoragePath = configuration["ProtectedStoragePath"] 
                ?? Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "ProtectedFiles");
        }

        public async Task<(string CanonicalId, string FilePath)> SaveProtectedFileAsync(string module, string parentId, IFormFile file)
        {
            if (file == null || file.Length == 0) throw new ArgumentException("File is empty", nameof(file));

            var safeModule = Path.GetFileName(module); // Prevent directory traversal
            var safeParentId = Path.GetFileName(parentId);
            
            var folderPath = Path.Combine(_baseStoragePath, safeModule, safeParentId);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // Generate safe internal filename
            var ext = Path.GetExtension(file.FileName).ToLower();
            var uniqueId = Guid.NewGuid().ToString("N");
            var internalFileName = $"{uniqueId}{ext}";
            var physicalPath = Path.Combine(folderPath, internalFileName);

            using (var stream = new FileStream(physicalPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Canonical ID will encode module, parentId, and unique filename
            var canonicalId = $"{safeModule}/{safeParentId}/{internalFileName}";
            
            return (canonicalId, physicalPath);
        }

        public async Task<(string CanonicalId, string FilePath)> SaveProtectedFileFromBytesAsync(string module, string parentId, byte[] fileBytes, string extension)
        {
            if (fileBytes == null || fileBytes.Length == 0) throw new ArgumentException("File bytes are empty", nameof(fileBytes));

            var safeModule = Path.GetFileName(module); // Prevent directory traversal
            var safeParentId = Path.GetFileName(parentId);
            
            var folderPath = Path.Combine(_baseStoragePath, safeModule, safeParentId);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var ext = string.IsNullOrEmpty(extension) ? ".bin" : extension.ToLower();
            if (!ext.StartsWith(".")) ext = "." + ext;

            var uniqueId = Guid.NewGuid().ToString("N");
            var internalFileName = $"{uniqueId}{ext}";
            var physicalPath = Path.Combine(folderPath, internalFileName);

            await File.WriteAllBytesAsync(physicalPath, fileBytes);

            var canonicalId = $"{safeModule}/{safeParentId}/{internalFileName}";
            
            return (canonicalId, physicalPath);
        }

        public Task<(string PhysicalPath, string ContentType, string OriginalName)> GetProtectedFileAsync(string canonicalId)
        {
            // Validate CanonicalId to prevent path traversal
            if (string.IsNullOrEmpty(canonicalId) || canonicalId.Contains("..") || Path.IsPathRooted(canonicalId))
            {
                throw new UnauthorizedAccessException("Invalid file identifier.");
            }

            var parts = canonicalId.Split('/');
            if (parts.Length != 3)
            {
                throw new ArgumentException("Invalid canonical identifier format.");
            }

            var physicalPath = Path.Combine(_baseStoragePath, parts[0], parts[1], parts[2]);

            if (!File.Exists(physicalPath))
            {
                throw new FileNotFoundException("Protected file not found", canonicalId);
            }

            // ContentType and OriginalName can be resolved via DB, but here we return basic content types based on ext
            var ext = Path.GetExtension(physicalPath).ToLower();
            var contentType = GetContentType(ext);
            var originalName = parts[2]; // Fallback, usually DB will provide the original name

            return Task.FromResult((physicalPath, contentType, originalName));
        }

        private string GetContentType(string extension)
        {
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream"
            };
        }
    }
}
