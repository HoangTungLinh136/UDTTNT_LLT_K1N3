using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using giao_dien_demo.Services;

namespace giao_dien_demo.Filters
{
    public class FileValidationFilter : IAsyncActionFilter
    {
        private readonly ISystemConfigService _configService;

        public FileValidationFilter(ISystemConfigService configService)
        {
            _configService = configService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var maxFileMBStr = await _configService.GetValueAsync("Storage.MaxFileSizeMB", "10");
            var allowedTypesStr = await _configService.GetValueAsync("Storage.AllowedTypes", ".jpg,.jpeg,.png,.pdf,.doc,.docx,.xls,.xlsx");

            if (!int.TryParse(maxFileMBStr, out int maxFileMB)) maxFileMB = 10;
            long maxFileBytes = maxFileMB * 1024L * 1024L;

            var allowedExtensions = allowedTypesStr.Split(',')
                                                   .Select(e => e.Trim().ToLower())
                                                   .Where(e => !string.IsNullOrEmpty(e))
                                                   .ToList();

            var allFiles = new List<IFormFile>();

            foreach (var arg in context.ActionArguments.Values)
            {
                if (arg is IFormFile singleFile)
                {
                    allFiles.Add(singleFile);
                }
                else if (arg is IEnumerable<IFormFile> multiFiles)
                {
                    allFiles.AddRange(multiFiles);
                }
            }

            if (context.HttpContext.Request.HasFormContentType && context.HttpContext.Request.Form.Files.Count > 0)
            {
                foreach (var file in context.HttpContext.Request.Form.Files)
                {
                    if (!allFiles.Contains(file))
                    {
                        allFiles.Add(file);
                    }
                }
            }

            foreach (var file in allFiles)
            {
                if (file.Length > maxFileBytes)
                {
                    context.Result = new BadRequestObjectResult(new { success = false, message = $"Tệp tải lên vượt quá dung lượng tối đa cho phép ({maxFileMB} MB)." });
                    return;
                }

                var extension = Path.GetExtension(file.FileName)?.ToLower();
                if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
                {
                    context.Result = new BadRequestObjectResult(new { success = false, message = $"Định dạng tệp không được hỗ trợ: {extension}. Chỉ cho phép: {allowedTypesStr}" });
                    return;
                }
            }

            await next();
        }
    }
}
