using System;

namespace giao_dien_demo.Models
{
    public class AttendancePeriod
    {
        public int Id { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        
        // OPEN, CLOSED
        public string Status { get; set; } = "OPEN";
        
        public DateTime? LockedAt { get; set; }
        public string? LockedBy { get; set; }
    }
}
