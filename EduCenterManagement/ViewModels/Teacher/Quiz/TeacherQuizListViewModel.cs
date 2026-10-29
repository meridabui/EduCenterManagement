using System.ComponentModel.DataAnnotations;
using EduCenterManagement.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduCenterManagement.ViewModels.Teacher.Quiz
{
    public class TeacherQuizListViewModel
    {
        public string CurrentFilter { get; set; } = "my"; // "my", "all", "others"
        public int? SelectedSubjectId { get; set; }
        public string? SearchQuery { get; set; }
        public int CurrentTeacherId { get; set; }
        public string CurrentTeacherName { get; set; } = string.Empty;

        public int TotalMyQuizzes { get; set; }
        public int TotalAllQuizzes { get; set; }
        public int TotalOtherQuizzes { get; set; }

        public List<SelectListItem> Subjects { get; set; } = new();
        public List<QuizListItemViewModel> Quizzes { get; set; } = new();
    }

    public class QuizListItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
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

        public int QuestionCount { get; set; }
        public decimal TotalPoints { get; set; }
        public int SubmittedAttemptsCount { get; set; }

        public bool IsActiveNow => Status == QuizStatus.Published &&
                                  DateTime.UtcNow >= AvailableFrom &&
                                  DateTime.UtcNow <= AvailableUntil;
    }
}
