using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.Repositories.Parent;
using EduCenterManagement.ViewModels.Parent;

namespace EduCenterManagement.Services.Parent;

public sealed class ParentAcademicService : IParentAcademicService
{
    private readonly IParentAcademicRepository _repository;
    private readonly TimeProvider _clock;

    public ParentAcademicService(
        IParentAcademicRepository repository,
        TimeProvider clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<ParentCommentsViewModel?> GetCommentsAsync(
        int parentUserId,
        int studentId,
        int? classId,
        CancellationToken cancellationToken = default)
    {
        var student = await _repository.GetAuthorizedStudentAsync(
            parentUserId, studentId, cancellationToken);
        if (student == null)
            return null;

        var allComments = await _repository.GetVisibleCommentsAsync(
            studentId, cancellationToken);
        var classes = allComments
            .Select(comment => comment.Class)
            .DistinctBy(classItem => classItem.Id)
            .OrderBy(classItem => classItem.Name)
            .ToList();
        var comments = classId.HasValue
            ? allComments.Where(comment => comment.ClassId == classId.Value).ToList()
            : allComments;

        return new ParentCommentsViewModel
        {
            Navigation = CreateNavigation(student, "comments"),
            SelectedClassId = classId,
            Classes = CreateClassOptions(classes),
            Comments = comments.Select(comment => new ParentCommentItemViewModel
            {
                ClassName = comment.Class.Name,
                SubjectName = comment.Class.Subject.Name,
                TeacherName = comment.Teacher.FullName,
                SessionLabel = comment.Session == null
                    ? "Toàn khóa học"
                    : $"Buổi {comment.Session.SessionNumber} ({comment.Session.SessionDate:dd/MM/yyyy})",
                Content = comment.Content,
                CreatedAt = comment.CreatedAt
            }).ToList()
        };
    }

    public async Task<ParentAttendanceViewModel?> GetAttendanceAsync(
        int parentUserId,
        int studentId,
        int? classId,
        CancellationToken cancellationToken = default)
    {
        var student = await _repository.GetAuthorizedStudentAsync(
            parentUserId, studentId, cancellationToken);
        if (student == null)
            return null;

        var classes = await _repository.GetConfirmedClassesAsync(
            studentId, cancellationToken);
        if (classId.HasValue && classes.All(item => item.Id != classId.Value))
            return null;

        var allSessions = await _repository.GetSessionsAsync(
            studentId, classId, null, null, cancellationToken);
        allSessions = allSessions
            .Where(session => session.Status != ClassSessionStatus.Cancelled)
            .ToList();
        var completedSessions = allSessions
            .Where(session => session.Status == ClassSessionStatus.Completed)
            .ToList();

        var records = await _repository.GetAttendanceRecordsAsync(
            studentId,
            completedSessions.Select(session => session.Id).ToArray(),
            cancellationToken);
        var recordsBySession = records.ToDictionary(record => record.SessionId);

        var items = completedSessions
            .OrderByDescending(session => session.SessionDate)
            .ThenByDescending(session => session.StartsAt)
            .Select(session =>
            {
                recordsBySession.TryGetValue(session.Id, out var record);
                var status = GetDisplayStatus(session, record);
                return new ParentAttendanceItemViewModel
                {
                    SessionNumber = session.SessionNumber,
                    SessionDate = session.SessionDate,
                    StartsAt = session.StartsAt,
                    EndsAt = session.EndsAt,
                    ClassName = session.Class.Name,
                    SubjectName = session.Class.Subject.Name,
                    StatusText = status.Text,
                    StatusCssClass = status.CssClass,
                    Note = record?.Note
                };
            })
            .ToList();

        var present = records.Count(record => record.Status == AttendanceStatus.Present);
        var absent = records.Count(record => record.Status == AttendanceStatus.Absent);
        var marked = present + absent;

        return new ParentAttendanceViewModel
        {
            Navigation = CreateNavigation(student, "attendance"),
            SelectedClassId = classId,
            Classes = CreateClassOptions(classes),
            TotalSessions = allSessions.Count,
            PresentCount = present,
            AbsentCount = absent,
            AttendanceRate = marked == 0
                ? 0
                : Math.Round(present * 100m / marked, 1),
            Items = items
        };
    }

    public async Task<ParentTimetableViewModel?> GetTimetableAsync(
        int parentUserId,
        int studentId,
        int? classId,
        DateOnly? weekStart,
        CancellationToken cancellationToken = default)
    {
        var student = await _repository.GetAuthorizedStudentAsync(
            parentUserId, studentId, cancellationToken);
        if (student == null)
            return null;

        var classes = await _repository.GetConfirmedClassesAsync(
            studentId, cancellationToken);
        if (classId.HasValue && classes.All(item => item.Id != classId.Value))
            return null;

        var now = _clock.GetLocalNow().DateTime;
        var today = DateOnly.FromDateTime(now);
        var start = StartOfWeek(weekStart ?? today);
        var end = start.AddDays(6);
        var sessions = await _repository.GetSessionsAsync(
            studentId, classId, start, end, cancellationToken);
        var timeSlots = await _repository.GetActiveTimeSlotsAsync(cancellationToken);
        var records = await _repository.GetAttendanceRecordsAsync(
            studentId,
            sessions.Select(session => session.Id).ToArray(),
            cancellationToken);
        var recordsBySession = records.ToDictionary(record => record.SessionId);

        var days = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var date = start.AddDays(offset);
                return new ParentTimetableDayViewModel
                {
                    Date = date,
                    DayName = GetVietnameseDayName(date.DayOfWeek),
                    IsToday = date == today,
                    Sessions = sessions
                        .Where(session => session.SessionDate == date)
                        .OrderBy(session => session.StartsAt)
                        .Select(session =>
                        {
                            recordsBySession.TryGetValue(session.Id, out var record);
                            var status = GetDisplayStatus(session, record);
                            return new ParentTimetableSessionViewModel
                            {
                                TimeSlotId = session.ClassSchedule?.TimeSlotId,
                                SessionNumber = session.SessionNumber,
                                StartsAt = session.StartsAt,
                                EndsAt = session.EndsAt,
                                ClassName = session.Class.Name,
                                SubjectName = session.Class.Subject.Name,
                                TeacherName = session.Class.Teacher?.FullName ?? "Chưa phân công",
                                StatusText = status.Text,
                                StatusCssClass = status.CssClass
                            };
                        })
                        .ToList()
                };
            })
            .ToList();

        return new ParentTimetableViewModel
        {
            Navigation = CreateNavigation(student, "timetable"),
            SelectedClassId = classId,
            Classes = CreateClassOptions(classes),
            WeekStart = start,
            WeekEnd = end,
            TimeSlots = timeSlots.Select(slot => new ParentTimeSlotViewModel
            {
                TimeSlotId = slot.Id,
                Name = slot.Name,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime
            }).ToList(),
            Days = days
        };
    }

    public async Task<ParentQuizResultsViewModel?> GetQuizResultsAsync(
        int parentUserId,
        int studentId,
        int? classId,
        CancellationToken cancellationToken = default)
    {
        var student = await _repository.GetAuthorizedStudentAsync(
            parentUserId, studentId, cancellationToken);
        if (student == null)
            return null;

        var allAttempts = await _repository.GetFinishedQuizAttemptsAsync(
            studentId, cancellationToken);
        var classes = allAttempts
            .Select(attempt => attempt.Quiz.Class)
            .DistinctBy(classItem => classItem.Id)
            .OrderBy(classItem => classItem.Name)
            .ToList();
        var attempts = classId.HasValue
            ? allAttempts.Where(attempt => attempt.Quiz.ClassId == classId.Value).ToList()
            : allAttempts;
        var quizIds = allAttempts.Select(attempt => attempt.QuizId).Distinct().ToArray();
        var totals = await _repository.GetQuizTotalPointsAsync(
            quizIds, cancellationToken);
        var bestByQuiz = allAttempts
            .GroupBy(attempt => attempt.QuizId)
            .ToDictionary(
                group => group.Key,
                group => group.Max(attempt => attempt.Score ?? 0));

        return new ParentQuizResultsViewModel
        {
            Navigation = CreateNavigation(student, "quiz"),
            SelectedClassId = classId,
            Classes = CreateClassOptions(classes),
            Attempts = attempts.Select(attempt =>
            {
                totals.TryGetValue(attempt.QuizId, out var totalPoints);
                var score = attempt.Score ?? 0;
                return new ParentQuizAttemptItemViewModel
                {
                    QuizId = attempt.QuizId,
                    QuizTitle = attempt.Quiz.Title,
                    ClassName = attempt.Quiz.Class.Name,
                    SubjectName = attempt.Quiz.Class.Subject.Name,
                    AttemptNumber = attempt.AttemptNumber,
                    Score = score,
                    TotalPoints = totalPoints,
                    Percentage = totalPoints <= 0
                        ? 0
                        : Math.Round(score * 100m / totalPoints, 1),
                    StatusText = GetQuizStatusText(attempt.Status),
                    CompletedAt = attempt.SubmittedAt ?? attempt.GradedAt,
                    IsBestAttempt = score == bestByQuiz[attempt.QuizId]
                };
            }).ToList()
        };
    }

    private static ParentChildNavigationViewModel CreateNavigation(
        StudentProfile student,
        string activeTab) => new()
        {
            StudentId = student.Id,
            StudentName = student.FullName,
            ActiveTab = activeTab
        };

    private static List<ParentClassFilterItemViewModel> CreateClassOptions(
        IEnumerable<Class> classes) => classes
        .Select(classItem => new ParentClassFilterItemViewModel
        {
            ClassId = classItem.Id,
            Label = $"{classItem.Name} ({classItem.Subject.Name})"
        })
        .ToList();

    private static (string Text, string CssClass) GetDisplayStatus(
        ClassSession session,
        AttendanceRecord? attendance)
    {
        if (session.Status == ClassSessionStatus.Cancelled)
            return ("Đã hủy", "secondary");

        if (attendance?.Status == AttendanceStatus.Present)
            return ("Có mặt", "success");

        if (attendance?.Status == AttendanceStatus.Absent)
            return ("Vắng mặt", "danger");

        if (session.Status == ClassSessionStatus.Completed)
            return ("Chưa điểm danh", "warning text-dark");

        if (session.Status == ClassSessionStatus.Rescheduled)
            return ("Đã đổi lịch", "info");

        return ("Chưa hoàn thành", "primary");
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var daysFromMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysFromMonday);
    }

    private static string GetVietnameseDayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Thứ Hai",
        DayOfWeek.Tuesday => "Thứ Ba",
        DayOfWeek.Wednesday => "Thứ Tư",
        DayOfWeek.Thursday => "Thứ Năm",
        DayOfWeek.Friday => "Thứ Sáu",
        DayOfWeek.Saturday => "Thứ Bảy",
        DayOfWeek.Sunday => "Chủ Nhật",
        _ => string.Empty
    };

    private static string GetQuizStatusText(QuizAttemptStatus status) => status switch
    {
        QuizAttemptStatus.Submitted => "Đã nộp",
        QuizAttemptStatus.AutoSubmitted => "Tự động nộp",
        QuizAttemptStatus.Expired => "Quá hạn",
        _ => status.ToString()
    };
}
