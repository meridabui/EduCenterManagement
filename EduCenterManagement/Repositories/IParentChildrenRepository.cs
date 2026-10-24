using EduCenterManagement.ViewModels.ParentChildren;

namespace EduCenterManagement.Repositories;

public interface IParentChildrenRepository
{
    Task<ParentChildrenViewModel?> GetChildrenAsync(int parentId, CancellationToken ct = default);

    Task<ChildDetailsViewModel?> GetDetailsAsync(int parentId, int studentId, CancellationToken ct = default);

    Task<AttendancePageViewModel?> GetAttendanceAsync(int parentId, int studentId, int? classId, int page, int pageSize, CancellationToken ct = default);
}