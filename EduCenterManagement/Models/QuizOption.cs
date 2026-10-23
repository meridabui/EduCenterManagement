
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduCenterManagement.Models
{
    [Table("quiz_options")]
    public class QuizOption
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int QuestionId { get; set; }

        [Required]
        public string OptionText { get; set; } = string.Empty;

        public bool IsCorrect { get; set; } = false;

        [Range(1, int.MaxValue)]
        public int SortOrder { get; set; }

        [ForeignKey(nameof(QuestionId))]
        public QuizQuestion Question { get; set; } = null!;
    }
}
