using EduCenterManagement.ViewModels.ParentChildren;
namespace EduCenterManagement.Services;
public interface IParentChildrenService { Task<ParentChildrenViewModel?> GetChildrenAsync(CancellationToken ct=default); Task<ChildDetailsViewModel?> GetDetailsAsync(int studentId,CancellationToken ct=default); Task<AttendancePageViewModel?> GetAttendanceAsync(int studentId,int? classId,int page,CancellationToken ct=default); }
