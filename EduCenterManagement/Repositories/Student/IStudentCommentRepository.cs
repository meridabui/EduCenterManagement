using EduCenterManagement.Models;

namespace EduCenterManagement.Repositories.Student
{
    public interface IStudentCommentRepository
    {
        Task<StudentProfile?> GetStudentProfileByUserIdAsync(int userId);

        // Lấy nhận xét giáo viên dành cho học sinh (bỏ qua nhận xét "teacher_only")
        Task<List<TeacherComment>> GetVisibleCommentsAsync(int studentId);
    }
}
