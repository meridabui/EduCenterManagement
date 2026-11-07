using EduCenterManagement.Enums;
using EduCenterManagement.Models;

namespace EduCenterManagement.Repositories.Teacher
{
    public interface ITeacherRepository
    {
        Task<TeacherProfile?> GetTeacherProfileByUserIdAsync(int userId);

        Task<List<Class>> GetAssignedClassesAsync(int teacherId);

        Task<List<ClassSchedule>> GetClassSchedulesAsync(List<int> classIds);

        Task<Dictionary<int, int>> GetEnrollmentCountsAsync(List<int> classIds);

        Task<Class?> GetClassByIdAndTeacherAsync(int classId, int teacherId);

        Task<List<Enrollment>> GetConfirmedStudentsByClassIdAsync(int classId);

        Task<List<TimeSlot>> GetActiveTimeSlotsAsync();

        Task<List<Class>> GetClassesInWeekRangeAsync(int teacherId, DateOnly weekStart, DateOnly weekEnd);

        Task<List<ClassSession>> GetActualSessionsInWeekRangeAsync(List<int> classIds, DateOnly weekStart, DateOnly weekEnd);

        Task<List<ClassSession>> GetSessionsByClassIdAsync(int classId);

        Task<List<ClassSchedule>> GetSchedulesByClassIdAsync(int classId);

        Task<bool> HasSessionsAsync(int classId);

        Task AddSessionsRangeAsync(List<ClassSession> sessions);

        Task<ClassSession?> GetSessionByIdAndTeacherAsync(int sessionId, int teacherId);

        Task<bool> UpdateSessionStatusAsync(int sessionId, int teacherId, ClassSessionStatus status);
    }
}

