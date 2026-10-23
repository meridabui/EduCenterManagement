
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduCenterManagement.Models
{
    [Table("quiz_answers")]
    public class QuizAnswer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AttemptId { get; set; }

        [Required]
        public int QuestionId { get; set; }

        public int? SelectedOptionId { get; set; }

        public bool? IsCorrect { get; set; }

        [Column(TypeName = "decimal(8,2)")]
        [Range(typeof(decimal), "0", "999999")]
        public decimal EarnedPoints { get; set; }

        public DateTime? AnsweredAt { get; set; }

        [ForeignKey(nameof(AttemptId))]
        public QuizAttempt Attempt { get; set; } = null!;

        [ForeignKey(nameof(QuestionId))]
        public QuizQuestion Question { get; set; } = null!;

        [ForeignKey(nameof(SelectedOptionId))]
        public QuizOption? SelectedOption { get; set; }
    }
}
