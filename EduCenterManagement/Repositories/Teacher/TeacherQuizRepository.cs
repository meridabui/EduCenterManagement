using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Repositories.Teacher
{
    public class TeacherQuizRepository : ITeacherQuizRepository
    {
        private readonly ApplicationDbContext _context;

        public TeacherQuizRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<TeacherProfile?> GetTeacherProfileByUserIdAsync(int userId)
        {
            return await _context.TeacherProfiles
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.UserId == userId);
        }

        public async Task<List<Subject>> GetAllSubjectsAsync()
        {
            return await _context.Subjects
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<(List<Quiz> Quizzes, int TotalMy, int TotalAll, int TotalOthers)> GetQuizzesFilteredAsync(
            int teacherId, string filter, int? subjectId, string? search)
        {
            var query = _context.Quizzes
                .AsNoTracking()
                .Include(q => q.CreatedByTeacher)
                .Include(q => q.Class)
                    .ThenInclude(c => c.Subject)
                .Include(q => q.Session)
                .AsQueryable();

            int totalMy = await _context.Quizzes.CountAsync(q => q.CreatedByTeacherId == teacherId);
            int totalAll = await _context.Quizzes.CountAsync();
            int totalOthers = await _context.Quizzes.CountAsync(q => q.CreatedByTeacherId != teacherId);

            var normalizedFilter = filter?.ToLower() ?? "my";
            query = normalizedFilter switch
            {
                "all" => query,
                "others" => query.Where(q => q.CreatedByTeacherId != teacherId),
                _ => query.Where(q => q.CreatedByTeacherId == teacherId)
            };

            if (subjectId.HasValue && subjectId.Value > 0)
            {
                query = query.Where(q => q.Class.SubjectId == subjectId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(q => q.Title.ToLower().Contains(term) ||
                                         q.CreatedByTeacher.FullName.ToLower().Contains(term) ||
                                         q.Class.Name.ToLower().Contains(term));
            }

            var list = await query.OrderByDescending(q => q.CreatedAt).ToListAsync();
            return (list, totalMy, totalAll, totalOthers);
        }

        public async Task<Dictionary<int, (int Count, decimal TotalPoints)>> GetQuestionStatsByQuizIdsAsync(List<int> quizIds)
        {
            return await _context.QuizQuestions
                .AsNoTracking()
                .Where(qq => quizIds.Contains(qq.QuizId))
                .GroupBy(qq => qq.QuizId)
                .Select(g => new
                {
                    QuizId = g.Key,
                    Count = g.Count(),
                    TotalPoints = g.Sum(x => x.Points)
                })
                .ToDictionaryAsync(x => x.QuizId, x => (x.Count, x.TotalPoints));
        }

        public async Task<Dictionary<int, int>> GetAttemptCountsByQuizIdsAsync(List<int> quizIds)
        {
            return await _context.QuizAttempts
                .AsNoTracking()
                .Where(qa => quizIds.Contains(qa.QuizId) && qa.Status != QuizAttemptStatus.InProgress)
                .GroupBy(qa => qa.QuizId)
                .Select(g => new { QuizId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.QuizId, x => x.Count);
        }

        public async Task<List<Class>> GetTeacherClassesAsync(int teacherId)
        {
            return await _context.Classes
                .AsNoTracking()
                .Where(c => c.TeacherId == teacherId && c.Status != ClassStatus.Cancelled)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Class?> GetClassByIdAndTeacherAsync(int classId, int teacherId)
        {
            return await _context.Classes.FirstOrDefaultAsync(c => c.Id == classId && c.TeacherId == teacherId);
        }

        public async Task<ClassSession?> GetSessionByIdAndTeacherAsync(int sessionId, int teacherId)
        {
            return await _context.ClassSessions
                .Include(s => s.Class)
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.Class.TeacherId == teacherId);
        }

        public async Task<List<ClassSession>> GetSessionsByClassIdAsync(int classId, int teacherId)
        {
            return await _context.ClassSessions
                .AsNoTracking()
                .Where(s => s.ClassId == classId && s.Class.TeacherId == teacherId)
                .OrderBy(s => s.SessionNumber)
                .ToListAsync();
        }

        public async Task<Quiz> AddQuizAsync(Quiz quiz)
        {
            _context.Quizzes.Add(quiz);
            await _context.SaveChangesAsync();
            return quiz;
        }

        public async Task<Quiz?> GetQuizWithDetailsByIdAsync(int quizId)
        {
            return await _context.Quizzes
                .AsNoTracking()
                .Include(q => q.CreatedByTeacher)
                .Include(q => q.Class)
                    .ThenInclude(c => c.Subject)
                .Include(q => q.Session)
                .FirstOrDefaultAsync(q => q.Id == quizId);
        }

        public async Task<Quiz?> GetQuizByIdAsync(int quizId)
        {
            return await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId);
        }

        public async Task<List<QuizQuestion>> GetQuestionsByQuizIdAsync(int quizId)
        {
            return await _context.QuizQuestions
                .AsNoTracking()
                .Where(qq => qq.QuizId == quizId)
                .OrderBy(qq => qq.SortOrder)
                .ThenBy(qq => qq.Id)
                .ToListAsync();
        }

        public async Task<List<QuizOption>> GetOptionsByQuestionIdsAsync(List<int> questionIds)
        {
            return await _context.QuizOptions
                .AsNoTracking()
                .Where(qo => questionIds.Contains(qo.QuestionId))
                .OrderBy(qo => qo.SortOrder)
                .ThenBy(qo => qo.Id)
                .ToListAsync();
        }

        public async Task<List<QuizAttempt>> GetAttemptsByQuizIdAsync(int quizId)
        {
            return await _context.QuizAttempts
                .AsNoTracking()
                .Where(a => a.QuizId == quizId)
                .Include(a => a.Student)
                .OrderByDescending(a => a.StartedAt)
                .ToListAsync();
        }

        public async Task AddQuestionWithOptionsAsync(QuizQuestion question, List<QuizOption> options)
        {
            _context.QuizQuestions.Add(question);
            await _context.SaveChangesAsync();

            foreach (var opt in options)
            {
                opt.QuestionId = question.Id;
                _context.QuizOptions.Add(opt);
            }
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteQuestionAsync(int questionId, int quizId, int teacherId)
        {
            var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId && q.CreatedByTeacherId == teacherId);
            if (quiz == null) return false;

            var question = await _context.QuizQuestions.FirstOrDefaultAsync(qq => qq.Id == questionId && qq.QuizId == quizId);
            if (question == null) return false;

            var answers = await _context.QuizAnswers.Where(a => a.QuestionId == questionId).ToListAsync();
            if (answers.Any()) _context.QuizAnswers.RemoveRange(answers);

            var options = await _context.QuizOptions.Where(o => o.QuestionId == questionId).ToListAsync();
            if (options.Any()) _context.QuizOptions.RemoveRange(options);

            _context.QuizQuestions.Remove(question);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteQuizCascadeAsync(int quizId, int teacherId)
        {
            var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId && q.CreatedByTeacherId == teacherId);
            if (quiz == null) return false;

            var attempts = await _context.QuizAttempts.Where(a => a.QuizId == quizId).ToListAsync();
            var attemptIds = attempts.Select(a => a.Id).ToList();
            if (attemptIds.Any())
            {
                var answers = await _context.QuizAnswers.Where(ans => attemptIds.Contains(ans.AttemptId)).ToListAsync();
                _context.QuizAnswers.RemoveRange(answers);
                _context.QuizAttempts.RemoveRange(attempts);
            }

            var questions = await _context.QuizQuestions.Where(q => q.QuizId == quizId).ToListAsync();
            var questionIds = questions.Select(q => q.Id).ToList();
            if (questionIds.Any())
            {
                var options = await _context.QuizOptions.Where(o => questionIds.Contains(o.QuestionId)).ToListAsync();
                _context.QuizOptions.RemoveRange(options);
                _context.QuizQuestions.RemoveRange(questions);
            }

            _context.Quizzes.Remove(quiz);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateQuizStatusAsync(int quizId, int teacherId, QuizStatus status)
        {
            var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId && q.CreatedByTeacherId == teacherId);
            if (quiz == null) return false;

            quiz.Status = status;
            quiz.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<QuizAttempt?> GetStudentAttemptWithDetailsAsync(int attemptId)
        {
            return await _context.QuizAttempts
                .AsNoTracking()
                .Include(a => a.Quiz)
                .Include(a => a.Student)
                .FirstOrDefaultAsync(a => a.Id == attemptId);
        }

        public async Task<List<QuizAnswer>> GetAnswersByAttemptIdAsync(int attemptId)
        {
            return await _context.QuizAnswers
                .AsNoTracking()
                .Where(ans => ans.AttemptId == attemptId)
                .ToListAsync();
        }

        public async Task<Quiz> CloneQuizAsync(Quiz sourceQuiz, Quiz newQuiz)
        {
            _context.Quizzes.Add(newQuiz);
            await _context.SaveChangesAsync();

            var sourceQuestions = await _context.QuizQuestions
                .AsNoTracking()
                .Where(qq => qq.QuizId == sourceQuiz.Id)
                .OrderBy(qq => qq.SortOrder)
                .ToListAsync();

            var sourceQIds = sourceQuestions.Select(q => q.Id).ToList();
            var sourceOptions = await _context.QuizOptions
                .AsNoTracking()
                .Where(qo => sourceQIds.Contains(qo.QuestionId))
                .ToListAsync();

            var now = DateTime.UtcNow;
            foreach (var sq in sourceQuestions)
            {
                var newQuestion = new QuizQuestion
                {
                    QuizId = newQuiz.Id,
                    QuestionText = sq.QuestionText,
                    Points = sq.Points,
                    SortOrder = sq.SortOrder,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.QuizQuestions.Add(newQuestion);
                await _context.SaveChangesAsync();

                var matchingOptions = sourceOptions.Where(o => o.QuestionId == sq.Id).ToList();
                foreach (var opt in matchingOptions)
                {
                    var newOption = new QuizOption
                    {
                        QuestionId = newQuestion.Id,
                        OptionText = opt.OptionText,
                        IsCorrect = opt.IsCorrect,
                        SortOrder = opt.SortOrder
                    };
                    _context.QuizOptions.Add(newOption);
                }
            }

            await _context.SaveChangesAsync();
            return newQuiz;
        }
    }
}
