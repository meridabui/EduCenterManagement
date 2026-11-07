using EduCenterManagement.Enums;
using EduCenterManagement.Models;

namespace EduCenterManagement.Repositories.Student
{
    public interface IStudentTimetableRepository
    {
        Task<StudentProfile?> GetStudentProfileByUserIdAsync(int userId);

        Task<List<int>> GetPaidClassIdsAsync(int studentId);

        Task<List<TimeSlot>> GetActiveTimeSlotsAsync();

        Task<List<ClassSession>> GetSessionsByWeekAsync(List<int> classIds, DateOnly startDate, DateOnly endDate);

        Task<Dictionary<int, AttendanceStatus>> GetAttendanceStatusesAsync(int studentId, List<int> sessionIds);

        Task<Dictionary<int, (int Count, int FirstSlideId)>> GetSlidesInfoAsync(List<int> sessionIds);

        Task<Dictionary<int, (int Count, int FirstQuizId)>> GetQuizzesInfoAsync(List<int> sessionIds);

        Task<HashSet<int>> GetAttemptedQuizIdsAsync(int studentId, List<int> quizIds);

        Task<int> GetUnreadNotificationCountAsync(int userId);

        Task<bool> HasPaidEnrollmentAsync(int studentId, int classId);

        Task<Class?> GetClassWithDetailsAsync(int classId);

        Task<List<ClassSession>> GetSessionsByClassIdAsync(int classId);

        Task<Slide?> GetSlideByIdAsync(int slideId);
    }
}
