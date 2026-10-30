using EduCenterManagement.Models;

namespace EduCenterManagement.Repositories.Teacher
{
    public interface ITeacherSlideRepository
    {
        Task<TeacherProfile?> GetTeacherProfileByUserIdAsync(int userId);
        Task<List<Class>> GetTeacherClassesAsync(int teacherId);
        Task<ClassSession?> GetSessionWithClassByIdAsync(int sessionId, int teacherId);
        Task<List<ClassSession>> GetSessionsByClassIdAsync(int classId, int teacherId);
        Task<List<Slide>> GetSlidesBySessionIdAsync(int sessionId);
        Task<Slide?> GetSlideByIdAsync(int slideId, int teacherId);
        Task AddSlideAsync(Slide slide);
        Task DeleteSlideAsync(Slide slide);
    }
}

