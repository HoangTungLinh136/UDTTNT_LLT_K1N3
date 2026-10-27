using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace giao_dien_demo.Models
{
    [Table("TrainingMaterials")]
    public class TrainingMaterial
    {
        [Key]
        public int Id { get; set; }

        public int CourseId { get; set; }
        [ForeignKey(nameof(CourseId))]
        public TrainingCourse? Course { get; set; }

        [Column("Name"), Required, StringLength(400)]
        public string Title { get; set; } = string.Empty;

        [Column("MaterialType"), Required, StringLength(100)]
        public string Type { get; set; } = string.Empty;

        [Column("ExternalUrl"), StringLength(2000)]
        public string? FileUrl { get; set; }

        [StringLength(520)]
        public string? OriginalFileName { get; set; }

        [StringLength(520)]
        public string? StoredFileName { get; set; }

        public bool IsPublished { get; set; } = true;
    }
}
