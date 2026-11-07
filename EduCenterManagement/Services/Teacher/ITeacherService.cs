using EduCenterManagement.Enums;
using EduCenterManagement.ViewModels.Teacher;

namespace EduCenterManagement.Services.Teacher
{
    public interface ITeacherService
    {
        Task<AssignedClassesViewModel?> GetAssignedClassesAsync(int userId);

        Task<StudentListViewModel?> GetStudentListAsync(int userId, int classId);

        Task<TeacherTimetableViewModel?> GetTimetableAsync(int userId, int weekOffset);

        Task<ClassSessionsViewModel?> GetSessionsAsync(int userId, int classId, string filter);

        Task<(bool Success, string Message, int SessionNumber)> UpdateSessionStatusAsync(int userId, int sessionId, int classId, ClassSessionStatus status);
    }
}

