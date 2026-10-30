using EduCenterManagement.Models;

namespace EduCenterManagement.Repositories.Teacher
{
    public interface ITeacherCommentRepository
    {
        Task<TeacherProfile?> GetTeacherProfileByUserIdAsync(int userId);
        Task<List<Class>> GetTeacherClassesAsync(int teacherId);
        Task<Class?> GetClassByIdAsync(int classId, int teacherId);
        Task<List<Enrollment>> GetConfirmedEnrollmentsByClassIdAsync(int classId);
        Task<List<ClassSession>> GetSessionsByClassIdAsync(int classId);
        Task<List<TeacherComment>> GetCommentsByClassIdAsync(int classId);
        Task<List<TeacherComment>> GetCommentsForStudentInClassAsync(int studentId, int classId);
        Task<TeacherComment?> GetCommentByIdAsync(int commentId, int teacherId);
        Task<StudentProfile?> GetStudentByIdAsync(int studentId);
        Task AddCommentAsync(TeacherComment comment);
        Task UpdateCommentAsync(TeacherComment comment);
        Task DeleteCommentAsync(TeacherComment comment);
    }
}
