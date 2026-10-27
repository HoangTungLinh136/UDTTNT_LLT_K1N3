using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    public class CameraConfig
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string IPAddress { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string RtspUrl { get; set; } = string.Empty;

        [StringLength(100)]
        public string Username { get; set; } = string.Empty;

        [StringLength(255)]
        public string Password { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }

    public class CameraEvent
    {
        [Key]
        public int Id { get; set; }

        public int CameraConfigId { get; set; }

        [ForeignKey("CameraConfigId")]
        public CameraConfig? CameraConfig { get; set; }

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string ZoneName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? SnapshotPath { get; set; }

        public bool IsResolved { get; set; } = false;
    }
}
