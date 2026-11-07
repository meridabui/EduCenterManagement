using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Repositories.Student
{
    public class StudentQuizRepository : IStudentQuizRepository
    {
        private readonly ApplicationDbContext _context;

        public StudentQuizRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<StudentProfile?> GetStudentProfileByUserIdAsync(int userId)
        {
            return await _context.StudentProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId);
        }

        public async Task<List<int>> GetPaidClassIdsAsync(int studentId)
        {
            return await _context.Enrollments
                .AsNoTracking()
                .Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Confirm)
                .Select(e => e.ClassId)
                .ToListAsync();
        }

        public async Task<List<Quiz>> GetVisibleQuizzesAsync(List<int> classIds)
        {
            return await _context.Quizzes
                .AsNoTracking()
                .Where(q => classIds.Contains(q.ClassId) && q.Status != QuizStatus.Draft)
                .Include(q => q.Class)
                    .ThenInclude(c => c.Subject)
                .Include(q => q.Session)
                .OrderByDescending(q => q.AvailableFrom)
                .ToListAsync();
        }

        public async Task<Quiz?> GetQuizByIdAsync(int quizId)
        {
            return await _context.Quizzes
                .AsNoTracking()
                .Include(q => q.Class)
                    .ThenInclude(c => c.Subject)
                .FirstOrDefaultAsync(q => q.Id == quizId);
        }

        public async Task<Dictionary<int, int>> GetQuestionCountsAsync(List<int> quizIds)
        {
            return await _context.QuizQuestions
                .AsNoTracking()
                .Where(q => quizIds.Contains(q.QuizId))
                .GroupBy(q => q.QuizId)
                .Select(g => new { QuizId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.QuizId, x => x.Count);
        }

        public async Task<Dictionary<int, decimal>> GetTotalPointsAsync(List<int> quizIds)
        {
            return await _context.QuizQuestions
                .AsNoTracking()
                .Where(q => quizIds.Contains(q.QuizId))
                .GroupBy(q => q.QuizId)
                .Select(g => new { QuizId = g.Key, Total = g.Sum(x => x.Points) })
                .ToDictionaryAsync(x => x.QuizId, x => x.Total);
        }

        public async Task<List<(QuizQuestion Question, List<QuizOption> Options)>> GetQuestionsWithOptionsAsync(int quizId)
        {
            var questions = await _context.QuizQuestions
                .AsNoTracking()
                .Where(q => q.QuizId == quizId)
                .OrderBy(q => q.SortOrder)
                .ToListAsync();

            var questionIds = questions.Select(q => q.Id).ToList();
            var options = await _context.QuizOptions
                .AsNoTracking()
                .Where(o => questionIds.Contains(o.QuestionId))
                .OrderBy(o => o.SortOrder)
                .ToListAsync();

            var optionsByQuestion = options
                .GroupBy(o => o.QuestionId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return questions
                .Select(q => (q, optionsByQuestion.TryGetValue(q.Id, out var opts) ? opts : new List<QuizOption>()))
                .ToList();
        }

        public async Task<List<QuizAttempt>> GetAttemptsAsync(int studentId, List<int> quizIds)
        {
            return await _context.QuizAttempts
                .AsNoTracking()
                .Where(a => a.StudentId == studentId && quizIds.Contains(a.QuizId))
                .OrderBy(a => a.AttemptNumber)
                .ToListAsync();
        }

        public async Task<QuizAttempt?> GetAttemptForUpdateAsync(int attemptId)
        {
            return await _context.QuizAttempts.FirstOrDefaultAsync(a => a.Id == attemptId);
        }

        public async Task<List<QuizAnswer>> GetAnswersByAttemptIdAsync(int attemptId)
        {
            return await _context.QuizAnswers
                .AsNoTracking()
                .Where(a => a.AttemptId == attemptId)
                .ToListAsync();
        }

        public async Task AddAttemptAsync(QuizAttempt attempt)
        {
            _context.QuizAttempts.Add(attempt);
            await _context.SaveChangesAsync();
        }

        public async Task AddAnswersAsync(IEnumerable<QuizAnswer> answers)
        {
            await _context.QuizAnswers.AddRangeAsync(answers);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
