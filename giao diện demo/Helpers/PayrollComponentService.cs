using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using giao_dien_demo.Models;

namespace giao_dien_demo.Helpers
{
    public class PayrollComponentService
    {
        private static readonly string FilePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "PayrollComponents.json");

        public static List<PayrollComponentRecord> GetAllComponents()
        {
            if (!File.Exists(FilePath))
            {
                // Seed default components if the file doesn't exist
                var defaultComps = new List<PayrollComponentRecord>
                {
                    new PayrollComponentRecord { Code = "WORKING_DAYS", Name = "Số ngày công", Category = "Công", DataType = "DAYS", SourceType = "SYNCED", Status = "ACTIVE", AllowsManualInput = true, IsRequired = true },
                    new PayrollComponentRecord { Code = "OT_HOURS", Name = "Số giờ OT", Category = "OT", DataType = "HOURS", SourceType = "SYNCED", Status = "ACTIVE", AllowsManualInput = true, IsRequired = false },
                    new PayrollComponentRecord { Code = "BASE_SALARY", Name = "Lương cơ bản", Category = "Lương", DataType = "MONEY", SourceType = "SYSTEM", Status = "ACTIVE", AllowsManualInput = false, IsRequired = true },
                    new PayrollComponentRecord { Code = "ALLOWANCE_POS", Name = "Phụ cấp chức vụ", Category = "Phụ cấp", DataType = "MONEY", SourceType = "MANUAL", Status = "ACTIVE", AllowsManualInput = true, IsRequired = false },
                    new PayrollComponentRecord { Code = "BONUS_PERF", Name = "Thưởng hiệu suất", Category = "Thưởng", DataType = "MONEY", SourceType = "MANUAL", Status = "ACTIVE", AllowsManualInput = true, IsRequired = false },
                    new PayrollComponentRecord { Code = "DEDUCT_PENALTY", Name = "Phạt vi phạm", Category = "Khấu trừ", DataType = "MONEY", SourceType = "MANUAL", Status = "ACTIVE", AllowsManualInput = true, IsRequired = false },
                    new PayrollComponentRecord { Code = "ADJUSTMENT_OTHER", Name = "Điều chỉnh khác", Category = "Điều chỉnh", DataType = "MONEY", SourceType = "MANUAL", Status = "ACTIVE", AllowsManualInput = true, IsRequired = false }
                };
                SaveAllComponents(defaultComps);
                return defaultComps;
            }

            try
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<List<PayrollComponentRecord>>(json) ?? new List<PayrollComponentRecord>();
            }
            catch
            {
                return new List<PayrollComponentRecord>();
            }
        }

        private static void SaveAllComponents(List<PayrollComponentRecord> components)
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(components, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }

        public static List<PayrollComponentRecord> GetActiveComponents()
        {
            return GetAllComponents().Where(c => c.Status == "ACTIVE").ToList();
        }

        public static PayrollComponentRecord SaveComponent(PayrollComponentRecord input, string updatedBy)
        {
            var allData = GetAllComponents();
            
            // Check formula circular dependency roughly
            if (input.SourceType == "FORMULA" && !string.IsNullOrWhiteSpace(input.Formula))
            {
                if (input.Formula.Contains(input.Code))
                {
                    throw new Exception("Công thức không được tham chiếu tới chính nó (Circular Dependency).");
                }
            }

            var existing = allData.FirstOrDefault(c => c.Code.Equals(input.Code, StringComparison.OrdinalIgnoreCase));
            
            if (existing != null)
            {
                existing.Name = input.Name;
                existing.Category = input.Category;
                existing.DataType = input.DataType;
                existing.SourceType = input.SourceType;
                existing.Formula = input.Formula;
                existing.CalculationOrder = input.CalculationOrder;
                existing.RoundingMode = input.RoundingMode;
                existing.IsTaxable = input.IsTaxable;
                existing.IsInsuranceApplicable = input.IsInsuranceApplicable;
                existing.Status = input.Status;
                existing.AllowsManualInput = input.SourceType == "MANUAL" || input.AllowsManualInput; // Tự động bật nếu Manual
                
                existing.UpdatedBy = updatedBy;
                existing.UpdatedAt = DateTime.Now;
                
                SaveAllComponents(allData);
                return existing;
            }
            else
            {
                input.Code = input.Code.Trim().ToUpper();
                input.CreatedBy = updatedBy;
                input.CreatedAt = DateTime.Now;
                input.UpdatedBy = updatedBy;
                input.UpdatedAt = DateTime.Now;
                
                input.AllowsManualInput = input.SourceType == "MANUAL" || input.AllowsManualInput;
                
                allData.Add(input);
                SaveAllComponents(allData);
                return input;
            }
        }
    }
}
