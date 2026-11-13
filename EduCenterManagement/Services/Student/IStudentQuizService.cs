using EduCenterManagement.ViewModels.Student;

namespace EduCenterManagement.Services.Student
{
    public interface IStudentQuizService
    {
        Task<StudentQuizListViewModel?> GetQuizListAsync(int userId);

        /// <summary>Bắt đầu lượt làm mới (hoặc trả về lượt đang làm dở).</summary>
        Task<(bool Success, string Message, int? AttemptId)> StartAttemptAsync(int userId, int quizId);

        Task<TakeQuizViewModel?> GetTakeQuizAsync(int userId, int attemptId);

        /// <param name="answers">QuestionId -> OptionId học sinh chọn.</param>
        Task<(bool Success, string Message)> SubmitAttemptAsync(int userId, int attemptId, Dictionary<int, int> answers, bool isAutoSubmit);

        Task<QuizResultViewModel?> GetResultAsync(int userId, int attemptId);

        // Danh sách tất cả lượt làm của học sinh cho một bài quiz
        Task<QuizAttemptHistoryViewModel?> GetAttemptHistoryAsync(int userId, int quizId);
    }
}
