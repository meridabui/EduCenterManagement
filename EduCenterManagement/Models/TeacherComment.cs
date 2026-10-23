
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduCenterManagement.Models
{
    [Table("teacher_comments")]
    public class TeacherComment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TeacherId { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Required]
        public int ClassId { get; set; }

        public int? SessionId { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Visibility { get; set; } = "parent_student";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(TeacherId))]
        public TeacherProfile Teacher { get; set; } = null!;

        [ForeignKey(nameof(StudentId))]
        public StudentProfile Student { get; set; } = null!;

        [ForeignKey(nameof(ClassId))]
        public Class Class { get; set; } = null!;

        [ForeignKey(nameof(SessionId))]
        public ClassSession? Session { get; set; }
    }
}
