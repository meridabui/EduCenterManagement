
using EduCenterManagement.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduCenterManagement.Models
{
    [Table("quiz_attempts")]
    public class QuizAttempt
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int QuizId { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Range(1, 100)]
        public int AttemptNumber { get; set; } = 1;

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; }

        public DateTime? SubmittedAt { get; set; }

        public DateTime? GradedAt { get; set; }

        public QuizAttemptStatus Status { get; set; } = QuizAttemptStatus.InProgress;
        [Column(TypeName = "decimal(8,2)")]
        [Range(typeof(decimal), "0", "999999")]
        public decimal? Score { get; set; }

        [ForeignKey(nameof(QuizId))]
        public Quiz Quiz { get; set; } = null!;

        [ForeignKey(nameof(StudentId))]
        public StudentProfile Student { get; set; } = null!;
    }
}
