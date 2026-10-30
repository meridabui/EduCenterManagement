using EduCenterManagement.Enums;
using EduCenterManagement.Models;

namespace EduCenterManagement.Repositories.Teacher
{
    public interface ITeacherAttendanceRepository
    {
        Task<TeacherProfile?> GetTeacherProfileByUserIdAsync(int userId);

        Task<List<Class>> GetTeacherClassesAsync(int teacherId);

        Task<ClassSession?> GetSessionWithClassByIdAsync(int sessionId, int teacherId);

        Task<List<ClassSession>> GetSessionsByClassIdAsync(int classId, int teacherId);

        Task<List<Enrollment>> GetConfirmedEnrollmentsByClassIdAsync(int classId);

        Task<List<AttendanceRecord>> GetAttendanceRecordsBySessionIdAsync(int sessionId);

        Task SaveAttendanceRecordsAsync(
            int sessionId,
            int teacherId,
            List<(int StudentId, AttendanceStatus Status, string? Note)> records,
            bool markSessionCompleted);
    }
}

