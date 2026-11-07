using EduCenterManagement.ViewModels.Parent;

namespace EduCenterManagement.Services.Parent;

public interface IParentAcademicService
{
    Task<ParentCommentsViewModel?> GetCommentsAsync(
        int parentUserId,
        int studentId,
        int? classId,
        CancellationToken cancellationToken = default);

    Task<ParentAttendanceViewModel?> GetAttendanceAsync(
        int parentUserId,
        int studentId,
        int? classId,
        CancellationToken cancellationToken = default);

    Task<ParentTimetableViewModel?> GetTimetableAsync(
        int parentUserId,
        int studentId,
        int? classId,
        DateOnly? weekStart,
        CancellationToken cancellationToken = default);

    Task<ParentQuizResultsViewModel?> GetQuizResultsAsync(
        int parentUserId,
        int studentId,
        int? classId,
        CancellationToken cancellationToken = default);
}
