using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Repositories.Parent;

public sealed class ParentAcademicRepository : IParentAcademicRepository
{
    private readonly ApplicationDbContext _context;

    public ParentAcademicRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<StudentProfile?> GetAuthorizedStudentAsync(
        int parentUserId,
        int studentId,
        CancellationToken cancellationToken = default)
    {
        return _context.ParentStudentLinks
            .AsNoTracking()
            .Where(link =>
                link.Parent.UserId == parentUserId
                && link.StudentId == studentId
                && link.Status == ParentStudentLinkStatus.Accepted)
            .Select(link => link.Student)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<List<TeacherComment>> GetVisibleCommentsAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        return _context.TeacherComments
            .AsNoTracking()
            .Where(comment =>
                comment.StudentId == studentId
                && comment.Visibility == "parent_student")
            .Include(comment => comment.Teacher)
            .Include(comment => comment.Class)
                .ThenInclude(classItem => classItem.Subject)
            .Include(comment => comment.Session)
            .OrderByDescending(comment => comment.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Class>> GetConfirmedClassesAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        return _context.Classes
            .AsNoTracking()
            .Where(classItem => _context.Enrollments.Any(enrollment =>
                enrollment.ClassId == classItem.Id
                && enrollment.StudentId == studentId
                && enrollment.Status == EnrollmentStatus.Confirm))
            .Include(classItem => classItem.Subject)
            .Include(classItem => classItem.Teacher)
            .OrderBy(classItem => classItem.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ClassSession>> GetSessionsAsync(
        int studentId,
        int? classId,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default)
    {
        var classIds = await _context.Enrollments
            .AsNoTracking()
            .Where(enrollment =>
                enrollment.StudentId == studentId
                && enrollment.Status == EnrollmentStatus.Confirm
                && (!classId.HasValue || enrollment.ClassId == classId.Value))
            .Select(enrollment => enrollment.ClassId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (classIds.Count == 0)
            return new List<ClassSession>();

        var query = _context.ClassSessions
            .AsNoTracking()
            .Where(session => classIds.Contains(session.ClassId));

        if (fromDate.HasValue)
            query = query.Where(session => session.SessionDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(session => session.SessionDate <= toDate.Value);

        return await query
            .Include(session => session.Class)
                .ThenInclude(classItem => classItem.Subject)
            .Include(session => session.Class)
                .ThenInclude(classItem => classItem.Teacher)
            .Include(session => session.ClassSchedule)
                .ThenInclude(schedule => schedule!.TimeSlot)
            .OrderBy(session => session.SessionDate)
            .ThenBy(session => session.StartsAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<TimeSlot>> GetActiveTimeSlotsAsync(
        CancellationToken cancellationToken = default)
    {
        return _context.TimeSlots
            .AsNoTracking()
            .Where(slot => slot.Status == TimeSlotStatus.Active)
            .OrderBy(slot => slot.StartTime)
            .ToListAsync(cancellationToken);
    }

    public Task<List<AttendanceRecord>> GetAttendanceRecordsAsync(
        int studentId,
        IReadOnlyCollection<int> sessionIds,
        CancellationToken cancellationToken = default)
    {
        if (sessionIds.Count == 0)
            return Task.FromResult(new List<AttendanceRecord>());

        return _context.AttendanceRecords
            .AsNoTracking()
            .Where(record =>
                record.StudentId == studentId
                && sessionIds.Contains(record.SessionId))
            .ToListAsync(cancellationToken);
    }

    public Task<List<QuizAttempt>> GetFinishedQuizAttemptsAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        return _context.QuizAttempts
            .AsNoTracking()
            .Where(attempt =>
                attempt.StudentId == studentId
                && (attempt.Status == QuizAttemptStatus.Submitted
                    || attempt.Status == QuizAttemptStatus.AutoSubmitted
                    || attempt.Status == QuizAttemptStatus.Expired))
            .Include(attempt => attempt.Quiz)
                .ThenInclude(quiz => quiz.Class)
                    .ThenInclude(classItem => classItem.Subject)
            .OrderByDescending(attempt => attempt.GradedAt ?? attempt.SubmittedAt ?? attempt.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<int, decimal>> GetQuizTotalPointsAsync(
        IReadOnlyCollection<int> quizIds,
        CancellationToken cancellationToken = default)
    {
        if (quizIds.Count == 0)
            return new Dictionary<int, decimal>();

        return await _context.QuizQuestions
            .AsNoTracking()
            .Where(question => quizIds.Contains(question.QuizId))
            .GroupBy(question => question.QuizId)
            .Select(group => new
            {
                QuizId = group.Key,
                TotalPoints = group.Sum(question => question.Points)
            })
            .ToDictionaryAsync(
                item => item.QuizId,
                item => item.TotalPoints,
                cancellationToken);
    }
}
