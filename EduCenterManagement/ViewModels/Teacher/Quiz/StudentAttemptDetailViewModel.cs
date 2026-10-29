using EduCenterManagement.Enums;

namespace EduCenterManagement.ViewModels.Teacher.Quiz
{
    public class StudentAttemptDetailViewModel
    {
        public int AttemptId { get; set; }
        public int QuizId { get; set; }
        public string QuizTitle { get; set; } = string.Empty;
        public string StudentFullName { get; set; } = string.Empty;
        public string StudentCode { get; set; } = string.Empty;
        public int AttemptNumber { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public QuizAttemptStatus Status { get; set; }
        public decimal? Score { get; set; }
        public decimal TotalPoints { get; set; }

        public List<StudentAnswerItemViewModel> Answers { get; set; } = new();
    }

    public class StudentAnswerItemViewModel
    {
        public int QuestionId { get; set; }
        public int SortOrder { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public decimal Points { get; set; }
        public decimal EarnedPoints { get; set; }
        public bool? IsCorrect { get; set; }

        public List<StudentAnswerOptionViewModel> Options { get; set; } = new();
    }

    public class StudentAnswerOptionViewModel
    {
        public int OptionId { get; set; }
        public string OptionText { get; set; } = string.Empty;
        public bool IsCorrectOption { get; set; }
        public bool IsSelectedByStudent { get; set; }
    }
}

