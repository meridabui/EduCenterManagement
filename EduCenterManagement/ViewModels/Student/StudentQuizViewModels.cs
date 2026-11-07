using EduCenterManagement.Enums;

namespace EduCenterManagement.ViewModels.Student
{
    public enum StudentQuizState
    {
        Upcoming,      // Chưa tới giờ mở
        Available,     // Có thể làm
        InProgress,    // Đang làm dở
        Completed,     // Đã hết lượt làm
        Ended          // Đã hết hạn / giáo viên đã đóng
    }

    public class StudentQuizListViewModel
    {
        public List<StudentQuizItemViewModel> Quizzes { get; set; } = new();
    }

    public class StudentQuizItemViewModel
    {
        public int QuizId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public int? SessionNumber { get; set; }
        public int DurationMinutes { get; set; }
        public DateTime AvailableFrom { get; set; }
        public DateTime AvailableUntil { get; set; }
        public int MaxAttempts { get; set; }
        public int AttemptsUsed { get; set; }
        public int QuestionCount { get; set; }
        public decimal TotalPoints { get; set; }
        public decimal? BestScore { get; set; }
        public int? InProgressAttemptId { get; set; }
        public int? LatestFinishedAttemptId { get; set; }
        public StudentQuizState State { get; set; }
    }

    public class TakeQuizViewModel
    {
        public int AttemptId { get; set; }
        public int QuizId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public int AttemptNumber { get; set; }
        public int MaxAttempts { get; set; }
        public DateTime ExpiresAt { get; set; }
        /// <summary>Số giây còn lại, tính ở server để tránh lệch giờ máy học sinh.</summary>
        public int RemainingSeconds { get; set; }
        public List<TakeQuizQuestionViewModel> Questions { get; set; } = new();
    }

    public class TakeQuizQuestionViewModel
    {
        public int QuestionId { get; set; }
        public int Number { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public decimal Points { get; set; }
        // Không chứa IsCorrect để không lộ đáp án ra trình duyệt
        public List<TakeQuizOptionViewModel> Options { get; set; } = new();
    }

    public class TakeQuizOptionViewModel
    {
        public int OptionId { get; set; }
        public string OptionText { get; set; } = string.Empty;
    }

    public class QuizResultViewModel
    {
        public int AttemptId { get; set; }
        public int QuizId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public int AttemptNumber { get; set; }
        public int MaxAttempts { get; set; }
        public QuizAttemptStatus Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public decimal Score { get; set; }
        public decimal TotalPoints { get; set; }
        public int CorrectCount { get; set; }
        public int QuestionCount { get; set; }
        /// <summary>Chỉ hiện đáp án đúng khi học sinh hết lượt làm hoặc quiz đã kết thúc.</summary>
        public bool ShowCorrectAnswers { get; set; }
        public bool CanRetake { get; set; }
        public List<QuizResultQuestionViewModel> Questions { get; set; } = new();
    }

    public class QuizResultQuestionViewModel
    {
        public int Number { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public decimal Points { get; set; }
        public decimal EarnedPoints { get; set; }
        public bool IsCorrect { get; set; }
        public int? SelectedOptionId { get; set; }
        public List<QuizResultOptionViewModel> Options { get; set; } = new();
    }

    public class QuizResultOptionViewModel
    {
        public int OptionId { get; set; }
        public string OptionText { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }
}
