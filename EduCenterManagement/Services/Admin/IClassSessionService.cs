using EduCenterManagement.Models;

namespace EduCenterManagement.Services.Admin;

public interface IClassSessionService
{
    Task<ClassSessionSyncResult> SynchronizeAsync(Class classItem, IReadOnlyCollection<ClassSchedule> schedules, bool onlyIfEmpty = false);
}
