namespace EduCenterManagement.Services.Common;

public interface ICurrentUserContext
{
    int? UserId { get; }
    Task<int?> GetStudentProfileIdAsync(CancellationToken ct = default);
    Task<int?> GetParentProfileIdAsync(CancellationToken ct = default);
    Task<int?> GetTeacherProfileIdAsync(CancellationToken ct = default);
}
