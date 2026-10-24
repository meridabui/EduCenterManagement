using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.ViewModels.ParentChildren;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Repositories;

public sealed class ParentChildResultsRepository(ApplicationDbContext db) : IParentChildResultsRepository
{
    public async Task<QuizResultsPageViewModel?> GetQuizResultsAsync(
        int parentProfileId,
        int studentId,
        int? classId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var linked = await db.ParentStudentLinks.AsNoTracking()
            .AnyAsync(x => x.ParentId == parentProfileId && x.StudentId == studentId && x.Status == ParentStudentLinkStatus.Accepted, ct);
        if (!linked) return null;

        var classes = await db.Enrollments.AsNoTracking()
            .Where(x => x.StudentId == studentId && x.Status == EnrollmentStatus.Confirmed)
            .Select(x => new AttendanceClassOptionViewModel { Id = x.ClassId, Name = x.Class.Name })
            .ToListAsync(ct);
        if (classId.HasValue && !classes.Any(x => x.Id == classId.Value)) return null;

        var student = await db.StudentProfiles.AsNoTracking().SingleAsync(x => x.Id == studentId, ct);
        var query = db.QuizAttempts.AsNoTracking().Where(x => x.StudentId == studentId && x.Score != null && (x.Status == QuizAttemptStatus.Submitted || x.Status == QuizAttemptStatus.AutoSubmitted));
        if (classId.HasValue) query = query.Where(x => x.Quiz.Session.ClassId == classId.Value);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.SubmittedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new QuizResultRowViewModel { QuizTitle = x.Quiz.Title, SubjectName = x.Quiz.Session.Class.Subject.Name, ClassName = x.Quiz.Session.Class.Name, SessionNumber = x.Quiz.Session.SessionNumber, AttemptNumber = x.AttemptNumber, Score = x.Score!.Value, MaxScore = db.QuizQuestions.Where(q => q.QuizId == x.QuizId).Sum(q => (decimal?)q.Points), SubmittedAt = x.SubmittedAt!.Value })
            .ToListAsync(ct);
        return new QuizResultsPageViewModel { StudentId = studentId, StudentName = student.FullName, Classes = classes, ClassId = classId, Page = page, TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)), Items = items };
    }
}
