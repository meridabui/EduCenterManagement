
using EduCenterManagement.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduCenterManagement.Models
{
    [Table("classes")]
    public class Class
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SubjectId { get; set; }

        public int? TeacherId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public DateOnly StartDate { get; set; }

        public DateOnly EndDate { get; set; }

        [Range(1, int.MaxValue)]
        public int MaxStudents { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0", "9999999999999999")]
        public decimal Fee { get; set; }

        public ClassStatus Status { get; set; } = ClassStatus.Draft;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(SubjectId))]
        public Subject Subject { get; set; } = null!;

        [ForeignKey(nameof(TeacherId))]
        public TeacherProfile? Teacher { get; set; }
    }
}
