using EduCenterManagement.Enums;
using EduCenterManagement.Models;

namespace EduCenterManagement.Repositories.Teacher
{
    public interface ITeacherQuizRepository
    {
        Task<TeacherProfile?> GetTeacherProfileByUserIdAsync(int userId);

        Task<List<Subject>> GetAllSubjectsAsync();

        Task<(List<Quiz> Quizzes, int TotalMy, int TotalAll, int TotalOthers)> GetQuizzesFilteredAsync(
            int teacherId, string filter, int? subjectId, string? search);

        Task<Dictionary<int, (int Count, decimal TotalPoints)>> GetQuestionStatsByQuizIdsAsync(List<int> quizIds);

        Task<Dictionary<int, int>> GetAttemptCountsByQuizIdsAsync(List<int> quizIds);

        Task<List<Class>> GetTeacherClassesAsync(int teacherId);

        Task<Class?> GetClassByIdAndTeacherAsync(int classId, int teacherId);

        Task<ClassSession?> GetSessionByIdAndTeacherAsync(int sessionId, int teacherId);

        Task<List<ClassSession>> GetSessionsByClassIdAsync(int classId, int teacherId);

        Task<Quiz> AddQuizAsync(Quiz quiz);

        Task<Quiz?> GetQuizWithDetailsByIdAsync(int quizId);

        Task<Quiz?> GetQuizByIdAsync(int quizId);

        Task<List<QuizQuestion>> GetQuestionsByQuizIdAsync(int quizId);

        Task<List<QuizOption>> GetOptionsByQuestionIdsAsync(List<int> questionIds);

        Task<List<QuizAttempt>> GetAttemptsByQuizIdAsync(int quizId);

        Task<List<StudentProfile>> GetEnrolledStudentsByClassIdAsync(int classId);

        Task AddQuestionWithOptionsAsync(QuizQuestion question, List<QuizOption> options);

        Task<bool> DeleteQuestionAsync(int questionId, int quizId, int teacherId);

        Task<bool> DeleteQuizCascadeAsync(int quizId, int teacherId);

        Task<bool> UpdateQuizStatusAsync(int quizId, int teacherId, QuizStatus status);

        Task<QuizAttempt?> GetStudentAttemptWithDetailsAsync(int attemptId);

        Task<List<QuizAnswer>> GetAnswersByAttemptIdAsync(int attemptId);

        Task<Quiz> CloneQuizAsync(Quiz sourceQuiz, Quiz newQuiz);

        Task<int> GetAttemptCountByQuizIdAsync(int quizId);

        Task<bool> UpdateQuizConfigAsync(int quizId, int teacherId, string title, int durationMinutes, DateTime availableFrom, DateTime availableUntil, int maxAttempts, bool hasAttempts);

        Task<bool> UpdateQuestionWithOptionsAsync(int questionId, int quizId, int teacherId, string questionText, decimal points, List<QuizOption> options);
    }
}
