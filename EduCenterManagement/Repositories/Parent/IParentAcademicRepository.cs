using EduCenterManagement.Models;

namespace EduCenterManagement.Repositories.Parent;

public interface IParentAcademicRepository
{
    Task<StudentProfile?> GetAuthorizedStudentAsync(
        int parentUserId,
        int studentId,
        CancellationToken cancellationToken = default);

    Task<List<TeacherComment>> GetVisibleCommentsAsync(
        int studentId,
        CancellationToken cancellationToken = default);

    Task<List<Class>> GetConfirmedClassesAsync(
        int studentId,
        CancellationToken cancellationToken = default);

    Task<List<ClassSession>> GetSessionsAsync(
        int studentId,
        int? classId,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default);

    Task<List<TimeSlot>> GetActiveTimeSlotsAsync(
        CancellationToken cancellationToken = default);

    Task<List<AttendanceRecord>> GetAttendanceRecordsAsync(
        int studentId,
        IReadOnlyCollection<int> sessionIds,
        CancellationToken cancellationToken = default);

    Task<List<QuizAttempt>> GetFinishedQuizAttemptsAsync(
        int studentId,
        CancellationToken cancellationToken = default);

    Task<Dictionary<int, decimal>> GetQuizTotalPointsAsync(
        IReadOnlyCollection<int> quizIds,
        CancellationToken cancellationToken = default);
}
