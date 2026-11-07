using EduCenterManagement.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduCenterManagement.Models
{
    [Table("quizzes")]
    public class Quiz
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClassId { get; set; }

        public int? SessionId { get; set; }

        [Required]
        public int CreatedByTeacherId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Range(1, 1440)]
        public int DurationMinutes { get; set; }

        public DateTime AvailableFrom { get; set; }

        public DateTime AvailableUntil { get; set; }

        public QuizStatus Status { get; set; } = QuizStatus.Draft;

        [Range(1, 100)]
        public int MaxAttempts { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(ClassId))]
        public Class Class { get; set; } = null!;

        [ForeignKey(nameof(SessionId))]
        public ClassSession? Session { get; set; }

        [ForeignKey(nameof(CreatedByTeacherId))]
        public TeacherProfile CreatedByTeacher { get; set; } = null!;
    }
}
