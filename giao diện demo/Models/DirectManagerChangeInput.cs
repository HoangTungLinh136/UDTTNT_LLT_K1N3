using System;

namespace giao_dien_demo.Models
{
    public class DirectManagerChangeInput
    {
        public int EmployeeId { get; set; }
        public int? NewManagerId { get; set; }
        public bool AllowNullManagerException { get; set; }
        public string? NullManagerReason { get; set; }
        public string? Reason { get; set; }
        public string? RowVersionString { get; set; }

        public byte[]? RowVersion
        {
            get
            {
                if (string.IsNullOrWhiteSpace(RowVersionString)) return null;
                try
                {
                    return Convert.FromBase64String(RowVersionString);
                }
                catch
                {
                    return null;
                }
            }
            set
            {
                RowVersionString = value != null ? Convert.ToBase64String(value) : null;
            }
        }
    }
}
