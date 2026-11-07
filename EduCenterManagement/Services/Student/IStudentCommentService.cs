using EduCenterManagement.ViewModels.Student;

namespace EduCenterManagement.Services.Student
{
    public interface IStudentCommentService
    {
        Task<StudentCommentListViewModel?> GetCommentsAsync(int userId, int? classId);
    }
}
