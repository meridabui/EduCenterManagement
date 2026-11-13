using EduCenterManagement.Enums;
using EduCenterManagement.Models;

namespace EduCenterManagement.Repositories.Admin;

public record AdminScheduleConflict(string ClassName, int DayOfWeek, string SlotName);
public record AdminCapacityStudent(string StudentName, int ParentId, DateTime? ReservedUntil, string Status);

public interface IAdminClassRepository
{
    Task<Class?> GetByIdAsync(int id);
    Task<Class?> GetWithSchedulesAsync(int id, bool tracking = false, bool includeSlots = false);
    Task<Class?> GetDetailsAsync(int id);
    Task<Class?> GetDeleteAsync(int id);
    Task<int> CountAsync(int? subjectId, int? teacherId, string? status);
    Task<List<Class>> GetPageAsync(int? subjectId, int? teacherId, string? status, int offset, int pageSize);
    Task<List<Subject>> GetSubjectsAsync(bool activeOnly = false);
    Task<List<TeacherProfile>> GetTeachersAsync(int? subjectId = null, bool activeOnly = false);
    Task<List<TimeSlot>> GetActiveSlotsAsync();
    Task<Dictionary<int, TimeSlot>> GetSlotsAsync(List<int> ids, bool tracking = true);
    Task<int> CountActiveSlotsAsync(List<int> ids);
    Task<bool> NameExistsAsync(string name, int exceptId);
    Task<bool> IsSubjectActiveAsync(int id);
    Task<bool> IsActiveTeacherForSubjectAsync(int teacherId, int subjectId);
    Task<List<AdminScheduleConflict>> FindConflictsAsync(int teacherId, int? currentClassId,
        DateOnly start, DateOnly end, List<int> days, TimeOnly startTime, TimeOnly endTime);
    Task<List<ClassSchedule>> GetOtherSchedulesAsync(int id, int? teacherId, DateOnly start, DateOnly end);
    Task<List<AdminCapacityStudent>> GetCapacityStudentsAsync(int id, DateTime now);
    Task<int> CountSessionsAsync(int id);
    Task<bool> HasConfirmedEnrollmentsAsync(int id);
    Task<bool> HasPaidPaymentsAsync(int id);
    Task<bool> HasSessionsAsync(int id);
    Task<bool> HasUnfinishedSessionsAsync(int id);
    Task<int> LockDraftAsync(int id);
    Task<int> LockForAssignmentAsync(int id);
    Task<int> LockForTransitionAsync(int id, ClassStatus source, ClassStatus target);
    void AddClass(Class classItem);
    void AddSchedule(ClassSchedule schedule);
    void AddSchedules(IEnumerable<ClassSchedule> schedules);
    void RemoveSchedules(IEnumerable<ClassSchedule> schedules);
    Task<List<ClassSession>> GetSessionsAsync(int classId);
    Task<List<int>> GetReferencedSessionIdsAsync(int classId);
    void RemoveSession(ClassSession session);
    void AddSessions(IEnumerable<ClassSession> sessions);
    Task<List<int>> GetCancellationRecipientsAsync(int id, int? teacherId);
    void AddNotifications(IEnumerable<Notification> notifications);
    Task CancelPendingEnrollmentsAsync(int id, DateTime now);
    Task CancelUnfinishedSessionsAsync(int id, DateTime now);
}
