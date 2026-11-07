using EduCenterManagement.Models;

namespace EduCenterManagement.Repositories.Student
{
    public interface IStudentQuizRepository
    {
        Task<StudentProfile?> GetStudentProfileByUserIdAsync(int userId);

        Task<List<int>> GetPaidClassIdsAsync(int studentId);

        /// <summary>Quiz đã phát hành/đã đóng thuộc các lớp đã thanh toán (kèm Class, Subject, Session).</summary>
        Task<List<Quiz>> GetVisibleQuizzesAsync(List<int> classIds);

        Task<Quiz?> GetQuizByIdAsync(int quizId);

        Task<Dictionary<int, int>> GetQuestionCountsAsync(List<int> quizIds);

        Task<Dictionary<int, decimal>> GetTotalPointsAsync(List<int> quizIds);

        /// <summary>Câu hỏi + đáp án của quiz, đã sắp theo SortOrder.</summary>
        Task<List<(QuizQuestion Question, List<QuizOption> Options)>> GetQuestionsWithOptionsAsync(int quizId);

        Task<List<QuizAttempt>> GetAttemptsAsync(int studentId, List<int> quizIds);

        /// <summary>Lấy attempt có tracking để cập nhật.</summary>
        Task<QuizAttempt?> GetAttemptForUpdateAsync(int attemptId);

        Task<List<QuizAnswer>> GetAnswersByAttemptIdAsync(int attemptId);

        Task AddAttemptAsync(QuizAttempt attempt);

        Task AddAnswersAsync(IEnumerable<QuizAnswer> answers);

        Task SaveChangesAsync();
    }
}
