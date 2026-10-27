using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class HrTaskHistory
    {
        public int Id { get; set; }
        public int HrTaskId { get; set; }
        [ForeignKey("HrTaskId")]
        public HrTask? HrTask { get; set; }

        public string? Action { get; set; }
        public string? Actor { get; set; }
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
