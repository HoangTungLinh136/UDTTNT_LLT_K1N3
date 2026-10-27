using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class PlanningPeriod
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Status { get; set; } // ACTIVE, CLOSED
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public ICollection<SuccessionPlan> SuccessionPlans { get; set; } = new List<SuccessionPlan>();
    }
}
