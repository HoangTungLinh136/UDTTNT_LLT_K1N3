using System;
using System.Collections.Generic;

namespace giao_dien_demo.Models
{
    public class PayrollTemplate
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public string Status { get; set; } = "ACTIVE"; // ACTIVE, INACTIVE, DRAFT
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        
        public List<PayrollTemplateColumn> Columns { get; set; } = new List<PayrollTemplateColumn>();
    }

    public class PayrollTemplateColumn
    {
        public string FieldKey { get; set; }
        public string DisplayName { get; set; }
        public bool IsVisible { get; set; }
        public int Order { get; set; }
        public bool IsRequired { get; set; }
    }

    public class PayrollTemplateFieldDto
    {
        public string FieldKey { get; set; }
        public string DefaultLabel { get; set; }
        public string Category { get; set; }
        public string DataType { get; set; } // NUMBER, MONEY, TEXT
        public bool IsRequired { get; set; }
        public bool DefaultVisible { get; set; }
        public int DefaultOrder { get; set; }
    }
}
