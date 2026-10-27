using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using Microsoft.EntityFrameworkCore;

namespace giao_dien_demo.Helpers
{
    public class PayrollInputService
    {
        private static readonly string FilePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "PayrollInputs.json");

        public static List<PayrollComponentRecord> GetAllowedComponents()
        {
            return PayrollComponentService.GetActiveComponents();
        }

        public static List<PayrollInputRecord> GetAllInputs()
        {
            if (!File.Exists(FilePath)) return new List<PayrollInputRecord>();
            try
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<List<PayrollInputRecord>>(json) ?? new List<PayrollInputRecord>();
            }
            catch
            {
                return new List<PayrollInputRecord>();
            }
        }

        private static void SaveAllInputs(List<PayrollInputRecord> inputs)
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(inputs, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }

        public static List<PayrollInputRecord> GetInputsByPeriod(string period)
        {
            return GetAllInputs().Where(i => i.PayrollPeriod == period).ToList();
        }

        public static async Task SyncAttendanceData(ApplicationDbContext context, string periodName, string updatedBy)
        {
            // Parse period
            if (!DateTime.TryParseExact("01/" + periodName, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime periodDate))
            {
                return; // Invalid period
            }
            
            var currentCycle = PayrollCycleHelper.GetCurrentCycle(periodDate);
            
            // Lấy chấm công
            var attendances = await context.Attendances
                .Where(x => x.CheckOut.HasValue && x.CheckIn >= currentCycle.StartDate && x.CheckIn <= currentCycle.EndDate)
                .ToListAsync();

            // Lấy Employee để lấy thông tin mapping
            var employees = await context.Employees.ToListAsync();

            var newInputs = new List<PayrollInputRecord>();
            
            var components = GetAllowedComponents();
            var workingDaysComp = components.First(c => c.Code == "WORKING_DAYS");
            var otHoursComp = components.First(c => c.Code == "OT_HOURS");
            var baseSalaryComp = components.First(c => c.Code == "BASE_SALARY");

            foreach (var emp in employees)
            {
                // Dữ liệu Hệ thống (SYSTEM)
                newInputs.Add(new PayrollInputRecord
                {
                    PayrollPeriod = periodName,
                    EmployeeId = emp.Id,
                    EmployeeCode = "EMP" + emp.Id.ToString("D3"),
                    EmployeeName = emp.Name,
                    DepartmentName = emp.Department ?? "Chưa phân bổ",
                    ComponentCode = baseSalaryComp.Code,
                    ComponentName = baseSalaryComp.Name,
                    Category = baseSalaryComp.Category,
                    Value = emp.Salary,
                    ValueFormatted = emp.Salary.ToString("N0") + " ₫",
                    SourceType = "SYSTEM",
                    Status = "VALID",
                    Note = "Lấy từ hồ sơ nhân sự",
                    UpdatedBy = "System",
                    UpdatedAt = DateTime.Now
                });

                // Dữ liệu Đồng bộ (SYNCED)
                var empAtts = attendances.Where(a => a.EmployeeId == emp.Id).ToList();
                if (empAtts.Any())
                {
                    decimal stdHours = (decimal)empAtts.Where(x => x.CheckIn.DayOfWeek != DayOfWeek.Sunday).Sum(x => x.StandardHours);
                    decimal otHours = (decimal)empAtts.Sum(x => x.OvertimeHours);
                    
                    decimal workingDays = Math.Round(stdHours / 8.0m, 2);

                    newInputs.Add(new PayrollInputRecord
                    {
                        PayrollPeriod = periodName,
                        EmployeeId = emp.Id,
                        EmployeeCode = "EMP" + emp.Id.ToString("D3"),
                        EmployeeName = emp.Name,
                        DepartmentName = emp.Department ?? "Chưa phân bổ",
                        ComponentCode = workingDaysComp.Code,
                        ComponentName = workingDaysComp.Name,
                        Category = workingDaysComp.Category,
                        Value = workingDays,
                        ValueFormatted = workingDays.ToString("0.##") + " ngày",
                        SourceType = "SYNCED",
                        Status = "VALID",
                        Note = "Đồng bộ từ máy chấm công",
                        UpdatedBy = updatedBy,
                        UpdatedAt = DateTime.Now
                    });

                    if (otHours > 0)
                    {
                        newInputs.Add(new PayrollInputRecord
                        {
                            PayrollPeriod = periodName,
                            EmployeeId = emp.Id,
                            EmployeeCode = "EMP" + emp.Id.ToString("D3"),
                            EmployeeName = emp.Name,
                            DepartmentName = emp.Department ?? "Chưa phân bổ",
                            ComponentCode = otHoursComp.Code,
                            ComponentName = otHoursComp.Name,
                            Category = otHoursComp.Category,
                            Value = otHours,
                            ValueFormatted = otHours.ToString("0.##") + " giờ",
                            SourceType = "SYNCED",
                            Status = "VALID",
                            Note = "Đồng bộ làm thêm giờ",
                            UpdatedBy = updatedBy,
                            UpdatedAt = DateTime.Now
                        });
                    }
                }
            }

            // Logic Ghi Đè (Upsert): Priority MANUAL > SYNCED/SYSTEM
            var allData = GetAllInputs();
            var periodData = allData.Where(x => x.PayrollPeriod == periodName).ToList();
            var otherData = allData.Where(x => x.PayrollPeriod != periodName).ToList();

            foreach (var ni in newInputs)
            {
                // Kiểm tra xem đã có record nào cho Employee + Component trong kỳ chưa
                var existing = periodData.FirstOrDefault(x => x.EmployeeId == ni.EmployeeId && x.ComponentCode == ni.ComponentCode);
                
                if (existing != null)
                {
                    // Đã có. Kiểm tra Source Priority. MANUAL không bao giờ bị ghi đè bởi SYNCED/SYSTEM
                    if (existing.SourceType == "MANUAL")
                    {
                        // Bỏ qua không update
                        continue;
                    }
                    else
                    {
                        // Ghi đè
                        existing.Value = ni.Value;
                        existing.ValueFormatted = ni.ValueFormatted;
                        existing.SourceType = ni.SourceType;
                        existing.UpdatedBy = ni.UpdatedBy;
                        existing.UpdatedAt = ni.UpdatedAt;
                        existing.Note = ni.Note;
                    }
                }
                else
                {
                    // Thêm mới
                    periodData.Add(ni);
                }
            }

            otherData.AddRange(periodData);
            SaveAllInputs(otherData);
        }

        public static PayrollInputRecord SaveManualInput(PayrollInputRecord input, string updatedBy, ApplicationDbContext context)
        {
            var components = GetAllowedComponents();
            var compMeta = components.FirstOrDefault(c => c.Code == input.ComponentCode);
            if (compMeta == null || !compMeta.AllowsManualInput) throw new Exception("Thành phần này không hợp lệ hoặc không cho phép nhập tay.");

            var allData = GetAllInputs();
            
            // Validate Employee
            var emp = context.Employees.FirstOrDefault(e => e.Id == input.EmployeeId);
            if (emp == null) throw new Exception("Nhân viên không tồn tại.");

            // Upsert with Priority MANUAL
            var existing = allData.FirstOrDefault(x => x.PayrollPeriod == input.PayrollPeriod && x.EmployeeId == input.EmployeeId && x.ComponentCode == input.ComponentCode);
            
            string formattedValue = input.Value.ToString();
            if (compMeta.DataType == "MONEY") formattedValue = input.Value.ToString("N0") + " ₫";
            else if (compMeta.DataType == "HOURS") formattedValue = input.Value.ToString("0.##") + " giờ";
            else if (compMeta.DataType == "DAYS") formattedValue = input.Value.ToString("0.##") + " ngày";

            if (existing != null)
            {
                // Luôn cho phép ghi đè lên các Source khác vì đây là Manual Input (Priority cao nhất)
                existing.Value = input.Value;
                existing.ValueFormatted = formattedValue;
                existing.SourceType = "MANUAL"; // Bắt buộc
                existing.UpdatedBy = updatedBy;
                existing.UpdatedAt = DateTime.Now;
                existing.Note = input.Note;
                SaveAllInputs(allData);
                return existing;
            }
            else
            {
                var newRecord = new PayrollInputRecord
                {
                    PayrollPeriod = input.PayrollPeriod,
                    EmployeeId = emp.Id,
                    EmployeeCode = "EMP" + emp.Id.ToString("D3"),
                    EmployeeName = emp.Name,
                    DepartmentName = emp.Department ?? "Chưa phân bổ",
                    ComponentCode = compMeta.Code,
                    ComponentName = compMeta.Name,
                    Category = compMeta.Category,
                    Value = input.Value,
                    ValueFormatted = formattedValue,
                    SourceType = "MANUAL",
                    Status = "VALID",
                    Note = input.Note,
                    UpdatedBy = updatedBy,
                    UpdatedAt = DateTime.Now
                };
                allData.Add(newRecord);
                SaveAllInputs(allData);
                return newRecord;
            }
        }

        public static PayrollInputKpi GetKPI(string period, ApplicationDbContext context)
        {
            var kpi = new PayrollInputKpi();
            
            // Total Employees (Scope)
            kpi.TotalEmployees = context.Employees.Count();
            
            var periodData = GetInputsByPeriod(period);
            
            // Count employees who have the required components
            var requiredComps = GetAllowedComponents().Where(c => c.IsRequired).Select(c => c.Code).ToList();
            
            int enoughCount = 0;
            var empGroup = periodData.GroupBy(p => p.EmployeeId);
            
            foreach (var g in empGroup)
            {
                var empComps = g.Select(x => x.ComponentCode).ToList();
                // Check if all required components are present
                if (requiredComps.All(rc => empComps.Contains(rc)))
                {
                    enoughCount++;
                }
            }

            kpi.EnoughDataCount = enoughCount;
            kpi.MissingDataCount = kpi.TotalEmployees - enoughCount;
            
            // Manual inputs count
            kpi.ManualInputCount = periodData.Count(p => p.SourceType == "MANUAL");

            return kpi;
        }
    }
}
