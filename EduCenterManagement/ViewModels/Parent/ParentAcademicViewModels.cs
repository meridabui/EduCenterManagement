namespace EduCenterManagement.ViewModels.Parent;

public sealed class ParentChildNavigationViewModel
{
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string ActiveTab { get; set; } = string.Empty;
}

public sealed class ParentClassFilterItemViewModel
{
    public int ClassId { get; set; }
    public string Label { get; set; } = string.Empty;
}

public sealed class ParentCommentsViewModel
{
    public ParentChildNavigationViewModel Navigation { get; set; } = new();
    public int? SelectedClassId { get; set; }
    public List<ParentClassFilterItemViewModel> Classes { get; set; } = new();
    public List<ParentCommentItemViewModel> Comments { get; set; } = new();
}

public sealed class ParentCommentItemViewModel
{
    public string ClassName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string SessionLabel { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class ParentAttendanceViewModel
{
    public ParentChildNavigationViewModel Navigation { get; set; } = new();
    public int? SelectedClassId { get; set; }
    public List<ParentClassFilterItemViewModel> Classes { get; set; } = new();
    public int TotalSessions { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public decimal AttendanceRate { get; set; }
    public List<ParentAttendanceItemViewModel> Items { get; set; } = new();
}

public sealed class ParentAttendanceItemViewModel
{
    public int SessionNumber { get; set; }
    public DateOnly SessionDate { get; set; }
    public TimeOnly StartsAt { get; set; }
    public TimeOnly EndsAt { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public string StatusCssClass { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public sealed class ParentTimetableViewModel
{
    public ParentChildNavigationViewModel Navigation { get; set; } = new();
    public int? SelectedClassId { get; set; }
    public List<ParentClassFilterItemViewModel> Classes { get; set; } = new();
    public DateOnly WeekStart { get; set; }
    public DateOnly WeekEnd { get; set; }
    public List<ParentTimeSlotViewModel> TimeSlots { get; set; } = new();
    public List<ParentTimetableDayViewModel> Days { get; set; } = new();
}

public sealed class ParentTimeSlotViewModel
{
    public int TimeSlotId { get; set; }
    public string Name { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

public sealed class ParentTimetableDayViewModel
{
    public DateOnly Date { get; set; }
    public string DayName { get; set; } = string.Empty;
    public bool IsToday { get; set; }
    public List<ParentTimetableSessionViewModel> Sessions { get; set; } = new();
}

public sealed class ParentTimetableSessionViewModel
{
    public int? TimeSlotId { get; set; }
    public int SessionNumber { get; set; }
    public TimeOnly StartsAt { get; set; }
    public TimeOnly EndsAt { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public string StatusCssClass { get; set; } = string.Empty;
}

public sealed class ParentQuizResultsViewModel
{
    public ParentChildNavigationViewModel Navigation { get; set; } = new();
    public int? SelectedClassId { get; set; }
    public List<ParentClassFilterItemViewModel> Classes { get; set; } = new();
    public List<ParentQuizAttemptItemViewModel> Attempts { get; set; } = new();
}

public sealed class ParentQuizAttemptItemViewModel
{
    public int QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public int AttemptNumber { get; set; }
    public decimal Score { get; set; }
    public decimal TotalPoints { get; set; }
    public decimal Percentage { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
    public bool IsBestAttempt { get; set; }
}
