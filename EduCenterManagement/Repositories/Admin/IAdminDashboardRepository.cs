using EduCenterManagement.Enums;
using EduCenterManagement.Models;

namespace EduCenterManagement.Repositories.Admin;

public record AdminEnrollmentCount(int ClassId, EnrollmentStatus Status, int Count);

public interface IAdminDashboardRepository
{
    Task<List<Class>> GetClassesAsync();
    Task<List<AdminEnrollmentCount>> GetEnrollmentCountsAsync(List<int> classIds, DateTime now);
    Task<int> CountTeachersAsync(bool activeOnly = false);
    Task<int> CountSubjectsAsync(bool activeOnly = false);
    Task<int> CountCancelledClassesAsync();
    Task<int> CountTimeSlotsAsync(bool activeOnly = false);
    Task<int> CountSchedulesAsync();
}
