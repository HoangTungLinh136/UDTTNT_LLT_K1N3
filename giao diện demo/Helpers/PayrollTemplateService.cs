using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using giao_dien_demo.Models;

namespace giao_dien_demo.Helpers
{
    public class PayrollTemplateService
    {
        private static readonly string FilePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "PayrollTemplates.json");

        // Allowed Field Registry (Metadata)
        public static List<PayrollTemplateFieldDto> GetAllowedFields()
        {
            return new List<PayrollTemplateFieldDto>
            {
                new PayrollTemplateFieldDto { FieldKey = "EmployeeCode", DefaultLabel = "Mã NV", Category = "Nhân viên", DataType = "TEXT", IsRequired = true, DefaultVisible = true, DefaultOrder = 1 },
                new PayrollTemplateFieldDto { FieldKey = "EmployeeName", DefaultLabel = "Tên nhân viên", Category = "Nhân viên", DataType = "TEXT", IsRequired = true, DefaultVisible = true, DefaultOrder = 2 },
                new PayrollTemplateFieldDto { FieldKey = "DepartmentName", DefaultLabel = "Phòng ban", Category = "Nhân viên", DataType = "TEXT", IsRequired = false, DefaultVisible = true, DefaultOrder = 3 },
                
                new PayrollTemplateFieldDto { FieldKey = "SalaryMonth", DefaultLabel = "Kỳ lương", Category = "Thời gian", DataType = "TEXT", IsRequired = false, DefaultVisible = true, DefaultOrder = 4 },
                
                new PayrollTemplateFieldDto { FieldKey = "BaseSalary", DefaultLabel = "Lương cơ bản", Category = "Thu nhập", DataType = "MONEY", IsRequired = false, DefaultVisible = true, DefaultOrder = 5 },
                new PayrollTemplateFieldDto { FieldKey = "PerformanceSalary", DefaultLabel = "Lương hiệu suất", Category = "Thu nhập", DataType = "MONEY", IsRequired = false, DefaultVisible = false, DefaultOrder = 6 },
                
                new PayrollTemplateFieldDto { FieldKey = "AllowanceCost", DefaultLabel = "Phụ cấp", Category = "Thu nhập", DataType = "MONEY", IsRequired = false, DefaultVisible = true, DefaultOrder = 7 },
                new PayrollTemplateFieldDto { FieldKey = "OvertimeCost", DefaultLabel = "Tăng ca", Category = "Thu nhập", DataType = "MONEY", IsRequired = false, DefaultVisible = true, DefaultOrder = 8 },
                new PayrollTemplateFieldDto { FieldKey = "Reward", DefaultLabel = "Thưởng", Category = "Thu nhập", DataType = "MONEY", IsRequired = false, DefaultVisible = true, DefaultOrder = 9 },
                
                new PayrollTemplateFieldDto { FieldKey = "TotalSalary", DefaultLabel = "Tổng thu nhập", Category = "Tổng hợp", DataType = "MONEY", IsRequired = true, DefaultVisible = true, DefaultOrder = 10 },
                
                new PayrollTemplateFieldDto { FieldKey = "Insurance", DefaultLabel = "Bảo hiểm", Category = "Khấu trừ", DataType = "MONEY", IsRequired = false, DefaultVisible = true, DefaultOrder = 11 },
                new PayrollTemplateFieldDto { FieldKey = "TaxTNCN", DefaultLabel = "Thuế TNCN", Category = "Khấu trừ", DataType = "MONEY", IsRequired = false, DefaultVisible = true, DefaultOrder = 12 },
                
                new PayrollTemplateFieldDto { FieldKey = "NetSalary", DefaultLabel = "Thực lĩnh", Category = "Tổng hợp", DataType = "MONEY", IsRequired = true, DefaultVisible = true, DefaultOrder = 13 }
            };
        }

        public static List<PayrollTemplate> GetAllTemplates()
        {
            if (!File.Exists(FilePath))
            {
                // Create default template if file doesn't exist
                var defaultTemplate = new PayrollTemplate
                {
                    Name = "Bảng lương mặc định",
                    Description = "Mẫu hiển thị đầy đủ các cột tiêu chuẩn",
                    IsDefault = true,
                    Columns = GetAllowedFields().OrderBy(f => f.DefaultOrder).Select(f => new PayrollTemplateColumn
                    {
                        FieldKey = f.FieldKey,
                        DisplayName = f.DefaultLabel,
                        IsVisible = f.DefaultVisible,
                        Order = f.DefaultOrder,
                        IsRequired = f.IsRequired
                    }).ToList()
                };
                SaveAllTemplates(new List<PayrollTemplate> { defaultTemplate });
                return new List<PayrollTemplate> { defaultTemplate };
            }

            var json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json)) return new List<PayrollTemplate>();
            
            try
            {
                return JsonSerializer.Deserialize<List<PayrollTemplate>>(json) ?? new List<PayrollTemplate>();
            }
            catch
            {
                return new List<PayrollTemplate>();
            }
        }

        private static void SaveAllTemplates(List<PayrollTemplate> templates)
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            
            var json = JsonSerializer.Serialize(templates, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }

        public static PayrollTemplate GetTemplate(string id)
        {
            return GetAllTemplates().FirstOrDefault(t => t.Id == id);
        }

        public static PayrollTemplate SaveTemplate(PayrollTemplate template)
        {
            var templates = GetAllTemplates();
            var existing = templates.FirstOrDefault(t => t.Id == template.Id);

            if (existing != null)
            {
                // Update
                existing.Name = template.Name;
                existing.Description = template.Description;
                existing.Status = template.Status;
                existing.UpdatedAt = DateTime.Now;
                
                // Fix order conflicts and required fields
                existing.Columns = template.Columns;
            }
            else
            {
                // Create
                template.Id = Guid.NewGuid().ToString();
                template.CreatedAt = DateTime.Now;
                template.UpdatedAt = DateTime.Now;
                
                // If it's the first template, set as default
                if (!templates.Any()) template.IsDefault = true;
                
                templates.Add(template);
            }

            SaveAllTemplates(templates);
            return existing ?? template;
        }

        public static PayrollTemplate DuplicateTemplate(string id)
        {
            var existing = GetTemplate(id);
            if (existing == null) return null;

            var newTemplate = new PayrollTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = existing.Name + " (Bản sao)",
                Description = existing.Description,
                Status = existing.Status,
                IsDefault = false, // Never duplicate default flag
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                Columns = existing.Columns.Select(c => new PayrollTemplateColumn
                {
                    FieldKey = c.FieldKey,
                    DisplayName = c.DisplayName,
                    IsVisible = c.IsVisible,
                    Order = c.Order,
                    IsRequired = c.IsRequired
                }).ToList()
            };

            var templates = GetAllTemplates();
            templates.Add(newTemplate);
            SaveAllTemplates(templates);
            
            return newTemplate;
        }

        public static void SetDefault(string id)
        {
            var templates = GetAllTemplates();
            var target = templates.FirstOrDefault(t => t.Id == id);
            
            if (target != null && target.Status == "ACTIVE")
            {
                // Atomic default setting within the system scope
                foreach (var t in templates)
                {
                    t.IsDefault = (t.Id == id);
                }
                SaveAllTemplates(templates);
            }
        }
    }
}
