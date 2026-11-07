using System.ComponentModel.DataAnnotations;
using EduCenterManagement.Enums;

namespace EduCenterManagement.ViewModels.Teacher.Quiz
{
    public class QuizDetailViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;

        public int? SessionId { get; set; }
        public int? SessionNumber { get; set; }
        public DateOnly? SessionDate { get; set; }

        public int CreatedByTeacherId { get; set; }
        public string CreatedByTeacherName { get; set; } = string.Empty;
        public bool IsMine { get; set; }

        public int DurationMinutes { get; set; }
        public DateTime AvailableFrom { get; set; }
        public DateTime AvailableUntil { get; set; }
        public int MaxAttempts { get; set; }
        public QuizStatus Status { get; set; }

        public decimal TotalPoints { get; set; }
        public string ActiveTab { get; set; } = "questions"; // "questions" or "results"

        public List<QuizQuestionDetailViewModel> Questions { get; set; } = new();
        public List<QuizAttemptDetailViewModel> Attempts { get; set; } = new();
    }

    public class QuizQuestionDetailViewModel
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public decimal Points { get; set; }
        public List<QuizOptionDetailViewModel> Options { get; set; } = new();
    }

    public class QuizOptionDetailViewModel
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
        public string OptionText { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }

    public class QuizAttemptDetailViewModel
    {
        public int AttemptId { get; set; }
        public int StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string StudentFullName { get; set; } = string.Empty;
        public int AttemptNumber { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public QuizAttemptStatus Status { get; set; }
        public decimal? Score { get; set; }
        public decimal MaxScore { get; set; }
    }
}
