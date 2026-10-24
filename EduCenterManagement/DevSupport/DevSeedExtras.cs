// TEMP: remove before merge to main
using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.DevSupport;

public static class DevSeedExtras
{
    public static async Task SeedAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        var parent = await db.ParentProfiles.SingleAsync(x => x.User.Email == "seed.parent@ecm.test", ct);
        var students = await db.ParentStudentLinks
            .Where(x => x.ParentId == parent.Id && x.Status == ParentStudentLinkStatus.Accepted)
            .Select(x => x.Student)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        var classes = await db.Classes.OrderBy(x => x.Id).ToListAsync(ct);

        foreach (var student in students)
        {
            foreach (var classItem in classes.Take(2))
            {
                if (!await db.Enrollments.AnyAsync(x => x.StudentId == student.Id && x.ClassId == classItem.Id && x.Status == EnrollmentStatus.Confirmed, ct))
                {
                    db.Enrollments.Add(new Enrollment { StudentId = student.Id, ClassId = classItem.Id, RegisteredByParentId = parent.Id, Status = EnrollmentStatus.Confirmed });
                }
            }
        }
        await db.SaveChangesAsync(ct);

        var firstStudent = students[0];
        var attendanceClasses = classes.Take(2).ToList();
        for (var number = 1; number <= 14; number++)
        {
            var classItem = attendanceClasses[number % attendanceClasses.Count];
            var sessionNumber = 100 + number;
            var session = await db.ClassSessions.SingleOrDefaultAsync(x => x.ClassId == classItem.Id && x.SessionNumber == sessionNumber, ct);
            if (session is null)
            {
                session = new ClassSession { ClassId = classItem.Id, SessionDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-number), StartsAt = new TimeOnly(8, 0), EndsAt = new TimeOnly(9, 30), SessionNumber = sessionNumber, Status = ClassSessionStatus.Completed };
                db.ClassSessions.Add(session);
                await db.SaveChangesAsync(ct);
            }
            if (!await db.AttendanceRecords.AnyAsync(x => x.StudentId == firstStudent.Id && x.SessionId == session.Id, ct))
            {
                db.AttendanceRecords.Add(new AttendanceRecord { StudentId = firstStudent.Id, SessionId = session.Id, MarkedByTeacherId = classItem.TeacherId!.Value, Status = number % 3 == 0 ? AttendanceStatus.Absent : AttendanceStatus.Present, Note = "Seed attendance record." });
            }
        }
        await db.SaveChangesAsync(ct);

        foreach (var classItem in classes.Take(2))
        {
            var session = await db.ClassSessions.OrderBy(x => x.Id).FirstOrDefaultAsync(x => x.ClassId == classItem.Id, ct);
            if (session is null)
            {
                session = new ClassSession { ClassId = classItem.Id, SessionDate = DateOnly.FromDateTime(DateTime.Today), StartsAt = new TimeOnly(8, 0), EndsAt = new TimeOnly(9, 30), SessionNumber = 500 + classItem.Id, Status = ClassSessionStatus.Completed };
                db.ClassSessions.Add(session);
                await db.SaveChangesAsync(ct);
            }
            var title = $"Seed Quiz {classItem.Id}";
            var quiz = await db.Quizzes.SingleOrDefaultAsync(x => x.Title == title, ct);
            if (quiz is null)
            {
                quiz = new Quiz { SessionId = session.Id, CreatedByTeacherId = classItem.TeacherId!.Value, Title = title, DurationMinutes = 30, AvailableFrom = DateTime.UtcNow.AddDays(-30), AvailableUntil = DateTime.UtcNow.AddDays(30), Status = QuizStatus.Published };
                db.Quizzes.Add(quiz);
                await db.SaveChangesAsync(ct);
            }
            if (!await db.QuizQuestions.AnyAsync(x => x.QuizId == quiz.Id, ct))
            {
                var question = new QuizQuestion { QuizId = quiz.Id, QuestionText = "Seed question", Points = 10, SortOrder = 1 };
                db.QuizQuestions.Add(question);
                await db.SaveChangesAsync(ct);
                db.QuizOptions.AddRange(new QuizOption { QuestionId = question.Id, OptionText = "Correct option", IsCorrect = true, SortOrder = 1 }, new QuizOption { QuestionId = question.Id, OptionText = "Other option", IsCorrect = false, SortOrder = 2 });
            }
        }
        await db.SaveChangesAsync(ct);

        var quizzes = await db.Quizzes.Where(x => x.Title.StartsWith("Seed Quiz ")).OrderBy(x => x.Id).Take(2).ToListAsync(ct);
        foreach (var student in students)
        {
            await AddAttemptAsync(db, quizzes[0], student.Id, 1, QuizAttemptStatus.Submitted, 8, ct);
            await AddAttemptAsync(db, quizzes[0], student.Id, 2, QuizAttemptStatus.AutoSubmitted, 9, ct);
            await AddAttemptAsync(db, quizzes[1], student.Id, 1, QuizAttemptStatus.Submitted, 7, ct);
            await AddAttemptAsync(db, quizzes[1], student.Id, 2, QuizAttemptStatus.InProgress, null, ct);
        }
    }

    private static async Task AddAttemptAsync(ApplicationDbContext db, Quiz quiz, int studentId, int attemptNumber, QuizAttemptStatus status, decimal? score, CancellationToken ct)
    {
        if (await db.QuizAttempts.AnyAsync(x => x.QuizId == quiz.Id && x.StudentId == studentId && x.AttemptNumber == attemptNumber, ct)) return;
        db.QuizAttempts.Add(new QuizAttempt { QuizId = quiz.Id, StudentId = studentId, AttemptNumber = attemptNumber, Status = status, Score = score, StartedAt = DateTime.UtcNow.AddDays(-attemptNumber), ExpiresAt = DateTime.UtcNow.AddDays(1), SubmittedAt = score.HasValue ? DateTime.UtcNow.AddDays(-attemptNumber) : null, GradedAt = score.HasValue ? DateTime.UtcNow : null });
        await db.SaveChangesAsync(ct);
    }
}
