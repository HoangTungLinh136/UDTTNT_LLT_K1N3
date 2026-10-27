using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using giao_dien_demo.Models;

namespace giao_dien_demo.Helpers
{
    public class PayrollSettingService
    {
        private static readonly string FilePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "PayrollSettings.json");

        public static List<PayrollSettingVersion> GetAllVersions()
        {
            if (!File.Exists(FilePath))
            {
                // Seed default ACTIVE configuration
                var defaultSettings = new List<PayrollSettingVersion>
                {
                    new PayrollSettingVersion
                    {
                        VersionName = "Cấu hình Mặc định Hệ thống",
                        Status = "ACTIVE",
                        EffectiveFrom = new DateTime(2025, 1, 1),
                        CycleType = "MONTHLY",
                        PeriodStartRule = "01",
                        PeriodEndRule = "CUOI_THANG", // Tượng trưng cho ngày cuối tháng
                        PayDateRule = "05",
                        HolidayHandlingRule = "PREVIOUS_WORKING_DAY",
                        SalaryRoundingMode = "ROUND",
                        SalaryRoundingStep = 1000,
                        AutoLockDays = 5,
                        CreatedBy = "System",
                        CreatedAt = DateTime.Now,
                        ActivatedBy = "System",
                        ActivatedAt = DateTime.Now
                    }
                };
                SaveAllVersions(defaultSettings);
                return defaultSettings;
            }

            try
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<List<PayrollSettingVersion>>(json) ?? new List<PayrollSettingVersion>();
            }
            catch
            {
                return new List<PayrollSettingVersion>();
            }
        }

        private static void SaveAllVersions(List<PayrollSettingVersion> versions)
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(versions, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }

        public static PayrollSettingVersion GetCurrentActiveVersion()
        {
            return GetAllVersions().OrderByDescending(v => v.EffectiveFrom).FirstOrDefault(v => v.Status == "ACTIVE");
        }
        
        public static PayrollSettingVersion GetVersion(string versionId)
        {
            return GetAllVersions().FirstOrDefault(v => v.VersionId == versionId);
        }

        public static PayrollSettingVersion CreateDraft(string user)
        {
            var active = GetCurrentActiveVersion();
            var allData = GetAllVersions();

            // Nếu đã có DRAFT thì không cho tạo thêm (hoặc ghi đè tùy policy)
            var existingDraft = allData.FirstOrDefault(v => v.Status == "DRAFT");
            if (existingDraft != null)
            {
                throw new Exception("Hệ thống đang có một phiên bản Nháp (DRAFT) chưa được kích hoạt. Hãy chỉnh sửa bản nháp đó.");
            }

            var draft = new PayrollSettingVersion
            {
                VersionName = "Bản nháp cấu hình mới",
                Status = "DRAFT",
                EffectiveFrom = null, // Chưa có hiệu lực
                
                CycleType = active?.CycleType ?? "MONTHLY",
                PeriodStartRule = active?.PeriodStartRule ?? "01",
                PeriodEndRule = active?.PeriodEndRule ?? "CUOI_THANG",
                
                PayDateRule = active?.PayDateRule ?? "05",
                HolidayHandlingRule = active?.HolidayHandlingRule ?? "PREVIOUS_WORKING_DAY",
                
                SalaryRoundingMode = active?.SalaryRoundingMode ?? "ROUND",
                SalaryRoundingStep = active?.SalaryRoundingStep ?? 1000,
                
                TaxConfigVersionId = active?.TaxConfigVersionId,
                InsuranceConfigVersionId = active?.InsuranceConfigVersionId,
                
                AutoLockDays = active?.AutoLockDays ?? 5,
                
                CreatedBy = user,
                CreatedAt = DateTime.Now
            };

            allData.Add(draft);
            SaveAllVersions(allData);
            return draft;
        }

        public static PayrollSettingVersion SaveDraft(PayrollSettingVersion input, string user)
        {
            var allData = GetAllVersions();
            var draft = allData.FirstOrDefault(v => v.VersionId == input.VersionId && v.Status == "DRAFT");
            
            if (draft == null) throw new Exception("Không tìm thấy phiên bản nháp hoặc phiên bản này đã bị khóa (Read-only).");

            draft.VersionName = input.VersionName;
            draft.CycleType = input.CycleType;
            draft.PeriodStartRule = input.PeriodStartRule;
            draft.PeriodEndRule = input.PeriodEndRule;
            draft.PayDateRule = input.PayDateRule;
            draft.HolidayHandlingRule = input.HolidayHandlingRule;
            draft.SalaryRoundingMode = input.SalaryRoundingMode;
            draft.SalaryRoundingStep = input.SalaryRoundingStep;
            draft.AutoLockDays = input.AutoLockDays;
            
            // Xóa tên người tạo cũ, đánh dấu update
            draft.CreatedBy = user;
            draft.CreatedAt = DateTime.Now;

            SaveAllVersions(allData);
            return draft;
        }

        public static PayrollSettingVersion ActivateVersion(string versionId, DateTime effectiveFrom, string user)
        {
            var allData = GetAllVersions();
            var draft = allData.FirstOrDefault(v => v.VersionId == versionId && v.Status == "DRAFT");
            
            if (draft == null) throw new Exception("Không tìm thấy phiên bản nháp hợp lệ.");

            // Đổi trạng thái các ACTIVE cũ thành SUPERSEDED (Đã bị thay thế)
            foreach (var v in allData.Where(x => x.Status == "ACTIVE"))
            {
                v.Status = "SUPERSEDED";
            }

            // Kích hoạt draft mới
            draft.Status = "ACTIVE";
            draft.EffectiveFrom = effectiveFrom;
            draft.ActivatedBy = user;
            draft.ActivatedAt = DateTime.Now;

            SaveAllVersions(allData);
            return draft;
        }
    }
}
