namespace EduCenterManagement.ViewModels.ParentChildren;

public sealed class QuizResultRowViewModel
{
    public string QuizTitle { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public int SessionNumber { get; init; }
    public int AttemptNumber { get; init; }
    public decimal Score { get; init; }
    public decimal? MaxScore { get; init; }
    public DateTime SubmittedAt { get; init; }
}

public sealed class QuizResultsPageViewModel
{
    public int StudentId { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public IReadOnlyList<QuizResultRowViewModel> Items { get; init; } = Array.Empty<QuizResultRowViewModel>();
    public IReadOnlyList<AttendanceClassOptionViewModel> Classes { get; init; } = Array.Empty<AttendanceClassOptionViewModel>();
    public int? ClassId { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
}
