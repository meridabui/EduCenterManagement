
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduCenterManagement.Models
{
    [Table("quiz_questions")]
    public class QuizQuestion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int QuizId { get; set; }

        [Required]
        public string QuestionText { get; set; } = string.Empty;

        [Column(TypeName = "decimal(8,2)")]
        [Range(typeof(decimal), "0.01", "999999")]
        public decimal Points { get; set; }

        [Range(1, int.MaxValue)]
        public int SortOrder { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(QuizId))]
        public Quiz Quiz { get; set; } = null!;
    }
}
