using EduCenterManagement.Enums;
using EduCenterManagement.ViewModels.Teacher.Quiz;

namespace EduCenterManagement.Services.Teacher
{
    public interface ITeacherQuizService
    {
        Task<TeacherQuizListViewModel?> GetQuizListAsync(int userId, string filter, int? subjectId, string? search);

        Task<CreateQuizViewModel?> GetCreateQuizViewModelAsync(int userId, int? sessionId, int? classId);

        Task<(bool Success, string Message, int QuizId)> CreateQuizAsync(int userId, CreateQuizViewModel model);

        Task<QuizDetailViewModel?> GetQuizDetailAsync(int userId, int quizId, string tab);

        Task<(bool Success, string Message)> AddQuestionAsync(int userId, QuizQuestionFormViewModel model);

        Task<(bool Success, string Message)> DeleteQuestionAsync(int userId, int questionId, int quizId);

        Task<CloneQuizViewModel?> GetCloneViewModelAsync(int userId, int sourceQuizId);

        Task<(bool Success, string Message, int NewQuizId)> CloneQuizAsync(int userId, CloneQuizViewModel model);

        Task<(bool Success, string Message)> UpdateStatusAsync(int userId, int quizId, QuizStatus status);

        Task<(bool Success, string Message)> DeleteQuizAsync(int userId, int quizId);

        Task<StudentAttemptDetailViewModel?> GetStudentAttemptDetailAsync(int userId, int attemptId);

        Task<List<object>> GetSessionsByClassAsync(int userId, int classId);
    }
}

