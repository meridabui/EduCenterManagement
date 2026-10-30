using EduCenterManagement.ViewModels.Teacher.Comment;

namespace EduCenterManagement.Services.Teacher
{
    public interface ITeacherCommentService
    {
        Task<ClassStudentCommentListViewModel?> GetClassStudentCommentListAsync(int userId, int? classId);
        Task<StudentCommentDetailViewModel?> GetStudentCommentDetailAsync(int userId, int classId, int studentId);
        Task<(bool Success, string Message)> CreateCommentAsync(int userId, CreateCommentInputModel model);
        Task<(bool Success, string Message)> UpdateCommentAsync(int userId, EditCommentInputModel model);
        Task<(bool Success, string Message)> DeleteCommentAsync(int userId, int commentId);
    }
}

