using System;
using System.Collections.Generic;

namespace giao_dien_demo.Models
{
    public class PayrollInputRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string PayrollPeriod { get; set; } // Ví dụ: "09/2026"
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; }
        public string EmployeeName { get; set; }
        public string DepartmentName { get; set; }

        public string ComponentCode { get; set; }
        public string ComponentName { get; set; }
        public string Category { get; set; } 

        public decimal Value { get; set; }
        public string ValueFormatted { get; set; } // Dùng cho UI

        public string SourceType { get; set; } // SYSTEM, SYNCED, MANUAL, IMPORTED
        public string Status { get; set; } // VALID, PENDING, ERROR
        public string Note { get; set; }

        public string UpdatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class PayrollComponentRecord
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Category { get; set; } // Lương, Phụ cấp, Thưởng, Khấu trừ, Công, Điều chỉnh
        public string DataType { get; set; } // MONEY, NUMBER, HOURS, DAYS
        public string SourceType { get; set; } // SYSTEM, SYNCED, MANUAL, FORMULA
        public string Formula { get; set; }
        public int CalculationOrder { get; set; }
        public string RoundingMode { get; set; } // NONE, ROUND, FLOOR, CEIL
        
        public bool IsTaxable { get; set; }
        public bool IsInsuranceApplicable { get; set; }
        
        public bool AllowsManualInput { get; set; }
        public bool IsRequired { get; set; }
        public string Status { get; set; } // ACTIVE, INACTIVE
        
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class PayrollInputKpi
    {
        public int TotalEmployees { get; set; }
        public int EnoughDataCount { get; set; }
        public int MissingDataCount { get; set; }
        public int ManualInputCount { get; set; }
    }
}
