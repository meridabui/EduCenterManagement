using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.Repositories.Student;
using EduCenterManagement.ViewModels.Student;

namespace EduCenterManagement.Services.Student
{
    public class StudentQuizService : IStudentQuizService
    {
        // Cho phép nộp trễ một chút so với hạn (độ trễ mạng / form tự nộp khi hết giờ)
        private static readonly TimeSpan SubmitGrace = TimeSpan.FromSeconds(30);

        private readonly IStudentQuizRepository _repo;

        public StudentQuizService(IStudentQuizRepository repo)
        {
            _repo = repo;
        }

        // Giờ mở/đóng quiz do giáo viên nhập theo giờ địa phương nên so sánh bằng DateTime.Now
        private static DateTime Now => DateTime.Now;

        public async Task<StudentQuizListViewModel?> GetQuizListAsync(int userId)
        {
            var student = await _repo.GetStudentProfileByUserIdAsync(userId);
            if (student == null) return null;

            var model = new StudentQuizListViewModel();

            var classIds = await _repo.GetPaidClassIdsAsync(student.Id);
            if (!classIds.Any()) return model;

            var quizzes = await _repo.GetVisibleQuizzesAsync(classIds);
            if (!quizzes.Any()) return model;

            var quizIds = quizzes.Select(q => q.Id).ToList();

            await FinalizeExpiredAttemptsAsync(student.Id, quizIds);

            var questionCounts = await _repo.GetQuestionCountsAsync(quizIds);
            var totalPoints = await _repo.GetTotalPointsAsync(quizIds);
            var attempts = await _repo.GetAttemptsAsync(student.Id, quizIds);
            var attemptsByQuiz = attempts.GroupBy(a => a.QuizId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var quiz in quizzes)
            {
                var myAttempts = attemptsByQuiz.TryGetValue(quiz.Id, out var list) ? list : new List<QuizAttempt>();
                var finished = myAttempts.Where(a => a.Status != QuizAttemptStatus.InProgress).ToList();
                var inProgress = myAttempts.FirstOrDefault(a => a.Status == QuizAttemptStatus.InProgress);
                int questionCount = questionCounts.GetValueOrDefault(quiz.Id);

                model.Quizzes.Add(new StudentQuizItemViewModel
                {
                    QuizId = quiz.Id,
                    Title = quiz.Title,
                    ClassName = quiz.Class.Name,
                    SubjectName = quiz.Class.Subject?.Name ?? "",
                    SessionNumber = quiz.Session?.SessionNumber,
                    DurationMinutes = quiz.DurationMinutes,
                    AvailableFrom = quiz.AvailableFrom,
                    AvailableUntil = quiz.AvailableUntil,
                    MaxAttempts = quiz.MaxAttempts,
                    AttemptsUsed = myAttempts.Count,
                    QuestionCount = questionCount,
                    TotalPoints = totalPoints.GetValueOrDefault(quiz.Id),
                    BestScore = finished.Any() ? finished.Max(a => a.Score ?? 0) : null,
                    InProgressAttemptId = inProgress?.Id,
                    LatestFinishedAttemptId = finished.OrderByDescending(a => a.AttemptNumber).FirstOrDefault()?.Id,
                    State = GetState(quiz, myAttempts.Count, inProgress != null, questionCount)
                });
            }

            // Ưu tiên: đang làm -> có thể làm -> sắp mở -> đã làm xong -> đã kết thúc
            model.Quizzes = model.Quizzes
                .OrderBy(q => q.State switch
                {
                    StudentQuizState.InProgress => 0,
                    StudentQuizState.Available => 1,
                    StudentQuizState.Upcoming => 2,
                    StudentQuizState.Completed => 3,
                    _ => 4
                })
                .ThenBy(q => q.AvailableUntil)
                .ToList();

            return model;
        }

        public async Task<(bool Success, string Message, int? AttemptId)> StartAttemptAsync(int userId, int quizId)
        {
            var student = await _repo.GetStudentProfileByUserIdAsync(userId);
            if (student == null) return (false, "Không tìm thấy hồ sơ học sinh.", null);

            var quiz = await _repo.GetQuizByIdAsync(quizId);
            if (quiz == null || quiz.Status == QuizStatus.Draft)
                return (false, "Bài quiz không tồn tại.", null);

            var classIds = await _repo.GetPaidClassIdsAsync(student.Id);
            if (!classIds.Contains(quiz.ClassId))
                return (false, "Bạn không thuộc lớp học của bài quiz này (chỉ dành cho lớp đã thanh toán).", null);

            await FinalizeExpiredAttemptsAsync(student.Id, new List<int> { quizId });

            var attempts = await _repo.GetAttemptsAsync(student.Id, new List<int> { quizId });

            // Đang làm dở thì cho làm tiếp, không tạo lượt mới
            var inProgress = attempts.FirstOrDefault(a => a.Status == QuizAttemptStatus.InProgress);
            if (inProgress != null) return (true, "", inProgress.Id);

            if (quiz.Status != QuizStatus.Published)
                return (false, "Bài quiz đã bị giáo viên đóng.", null);

            var now = Now;
            if (now < quiz.AvailableFrom)
                return (false, $"Bài quiz chưa mở. Thời gian mở: {quiz.AvailableFrom:dd/MM/yyyy HH:mm}.", null);
            if (now >= quiz.AvailableUntil)
                return (false, "Bài quiz đã hết hạn làm bài.", null);
            if (attempts.Count >= quiz.MaxAttempts)
                return (false, $"Bạn đã dùng hết {quiz.MaxAttempts} lượt làm bài.", null);

            var questionCounts = await _repo.GetQuestionCountsAsync(new List<int> { quizId });
            if (questionCounts.GetValueOrDefault(quizId) == 0)
                return (false, "Bài quiz chưa có câu hỏi nào.", null);

            // Hạn nộp = thời lượng làm bài, nhưng không vượt quá giờ đóng quiz
            var expiresAt = now.AddMinutes(quiz.DurationMinutes);
            if (expiresAt > quiz.AvailableUntil) expiresAt = quiz.AvailableUntil;

            var attempt = new QuizAttempt
            {
                QuizId = quizId,
                StudentId = student.Id,
                AttemptNumber = attempts.Count + 1,
                StartedAt = now,
                ExpiresAt = expiresAt,
                Status = QuizAttemptStatus.InProgress
            };

            await _repo.AddAttemptAsync(attempt);
            return (true, "", attempt.Id);
        }

        public async Task<TakeQuizViewModel?> GetTakeQuizAsync(int userId, int attemptId)
        {
            var (student, attempt) = await GetOwnAttemptAsync(userId, attemptId);
            if (student == null || attempt == null) return null;
            if (attempt.Status != QuizAttemptStatus.InProgress) return null;

            // Quá hạn mà chưa nộp -> chốt bài, controller sẽ chuyển sang trang kết quả
            if (Now > attempt.ExpiresAt + SubmitGrace)
            {
                await GradeAsync(attempt, new Dictionary<int, int>(), QuizAttemptStatus.Expired);
                return null;
            }

            var quiz = await _repo.GetQuizByIdAsync(attempt.QuizId);
            if (quiz == null) return null;

            var questions = await _repo.GetQuestionsWithOptionsAsync(quiz.Id);
            int remaining = (int)Math.Max(0, (attempt.ExpiresAt - Now).TotalSeconds);

            return new TakeQuizViewModel
            {
                AttemptId = attempt.Id,
                QuizId = quiz.Id,
                Title = quiz.Title,
                ClassName = quiz.Class.Name,
                AttemptNumber = attempt.AttemptNumber,
                MaxAttempts = quiz.MaxAttempts,
                ExpiresAt = attempt.ExpiresAt,
                RemainingSeconds = remaining,
                Questions = questions.Select((q, i) => new TakeQuizQuestionViewModel
                {
                    QuestionId = q.Question.Id,
                    Number = i + 1,
                    QuestionText = q.Question.QuestionText,
                    Points = q.Question.Points,
                    Options = q.Options.Select(o => new TakeQuizOptionViewModel
                    {
                        OptionId = o.Id,
                        OptionText = o.OptionText
                    }).ToList()
                }).ToList()
            };
        }

        public async Task<(bool Success, string Message)> SubmitAttemptAsync(
            int userId, int attemptId, Dictionary<int, int> answers, bool isAutoSubmit)
        {
            var (student, attempt) = await GetOwnAttemptAsync(userId, attemptId);
            if (student == null || attempt == null) return (false, "Không tìm thấy lượt làm bài.");
            if (attempt.Status != QuizAttemptStatus.InProgress) return (false, "Bài làm này đã được nộp trước đó.");

            if (Now > attempt.ExpiresAt + SubmitGrace)
            {
                // Nộp quá hạn: không chấp nhận đáp án
                await GradeAsync(attempt, new Dictionary<int, int>(), QuizAttemptStatus.Expired);
                return (false, "Đã quá thời gian làm bài, bài nộp không được chấp nhận.");
            }

            await GradeAsync(attempt, answers,
                isAutoSubmit ? QuizAttemptStatus.AutoSubmitted : QuizAttemptStatus.Submitted);
            return (true, isAutoSubmit ? "Hết giờ, hệ thống đã tự động nộp bài." : "Nộp bài thành công!");
        }

        public async Task<QuizResultViewModel?> GetResultAsync(int userId, int attemptId)
        {
            var (student, attempt) = await GetOwnAttemptAsync(userId, attemptId);
            if (student == null || attempt == null) return null;
            if (attempt.Status == QuizAttemptStatus.InProgress) return null;

            var quiz = await _repo.GetQuizByIdAsync(attempt.QuizId);
            if (quiz == null) return null;

            var questions = await _repo.GetQuestionsWithOptionsAsync(quiz.Id);
            var answers = (await _repo.GetAnswersByAttemptIdAsync(attempt.Id))
                .ToDictionary(a => a.QuestionId);
            var allAttempts = await _repo.GetAttemptsAsync(student.Id, new List<int> { quiz.Id });

            var now = Now;
            bool quizEnded = quiz.Status == QuizStatus.Closed || now >= quiz.AvailableUntil;
            bool noAttemptsLeft = allAttempts.Count >= quiz.MaxAttempts;
            bool canRetake = !quizEnded && !noAttemptsLeft
                && quiz.Status == QuizStatus.Published && now >= quiz.AvailableFrom;

            var resultQuestions = questions.Select((q, i) =>
            {
                answers.TryGetValue(q.Question.Id, out var ans);
                return new QuizResultQuestionViewModel
                {
                    Number = i + 1,
                    QuestionText = q.Question.QuestionText,
                    Points = q.Question.Points,
                    EarnedPoints = ans?.EarnedPoints ?? 0,
                    IsCorrect = ans?.IsCorrect == true,
                    SelectedOptionId = ans?.SelectedOptionId,
                    Options = q.Options.Select(o => new QuizResultOptionViewModel
                    {
                        OptionId = o.Id,
                        OptionText = o.OptionText,
                        IsCorrect = o.IsCorrect
                    }).ToList()
                };
            }).ToList();

            return new QuizResultViewModel
            {
                AttemptId = attempt.Id,
                QuizId = quiz.Id,
                Title = quiz.Title,
                ClassName = quiz.Class.Name,
                AttemptNumber = attempt.AttemptNumber,
                MaxAttempts = quiz.MaxAttempts,
                Status = attempt.Status,
                StartedAt = attempt.StartedAt,
                SubmittedAt = attempt.SubmittedAt,
                Score = attempt.Score ?? 0,
                TotalPoints = questions.Sum(q => q.Question.Points),
                CorrectCount = resultQuestions.Count(q => q.IsCorrect),
                QuestionCount = resultQuestions.Count,
                // Còn lượt làm lại thì chưa hiện đáp án đúng, tránh học sinh chép đáp án cho lượt sau
                ShowCorrectAnswers = quizEnded || noAttemptsLeft,
                CanRetake = canRetake,
                Questions = resultQuestions
            };
        }

        public async Task<QuizAttemptHistoryViewModel?> GetAttemptHistoryAsync(int userId, int quizId)
        {
            var student = await _repo.GetStudentProfileByUserIdAsync(userId);
            if (student == null) return null;

            var quiz = await _repo.GetQuizByIdAsync(quizId);
            if (quiz == null || quiz.Status == QuizStatus.Draft) return null;

            var classIds = await _repo.GetPaidClassIdsAsync(student.Id);
            if (!classIds.Contains(quiz.ClassId)) return null;

            await FinalizeExpiredAttemptsAsync(student.Id, new List<int> { quizId });

            var attempts = await _repo.GetAttemptsAsync(student.Id, new List<int> { quizId });

            var totalPoints = (await _repo.GetTotalPointsAsync(new List<int> { quizId })).GetValueOrDefault(quizId);

            var now = Now;
            bool canRetake = quiz.Status == QuizStatus.Published
                && now >= quiz.AvailableFrom && now < quiz.AvailableUntil
                && attempts.Count < quiz.MaxAttempts
                && attempts.All(a => a.Status != QuizAttemptStatus.InProgress);

            return new QuizAttemptHistoryViewModel
            {
                QuizId = quiz.Id,
                Title = quiz.Title,
                ClassName = quiz.Class.Name,
                MaxAttempts = quiz.MaxAttempts,
                TotalPoints = totalPoints,
                CanRetake = canRetake,
                Attempts = attempts
                    .OrderBy(a => a.AttemptNumber)
                    .Select(a => new QuizAttemptHistoryItemViewModel
                    {
                        AttemptId = a.Id,
                        AttemptNumber = a.AttemptNumber,
                        Status = a.Status,
                        StartedAt = a.StartedAt,
                        SubmittedAt = a.SubmittedAt,
                        Score = a.Status == QuizAttemptStatus.InProgress ? null : (a.Score ?? 0)
                    })
                    .ToList()
            };
        }

        // ===== Helpers =====

        private static StudentQuizState GetState(Quiz quiz, int attemptsUsed, bool hasInProgress, int questionCount)
        {
            if (hasInProgress) return StudentQuizState.InProgress;

            var now = Now;
            if (quiz.Status == QuizStatus.Closed || now >= quiz.AvailableUntil) return StudentQuizState.Ended;
            if (attemptsUsed >= quiz.MaxAttempts) return StudentQuizState.Completed;
            if (now < quiz.AvailableFrom || questionCount == 0) return StudentQuizState.Upcoming;
            return StudentQuizState.Available;
        }

        private async Task<(StudentProfile? Student, QuizAttempt? Attempt)> GetOwnAttemptAsync(int userId, int attemptId)
        {
            var student = await _repo.GetStudentProfileByUserIdAsync(userId);
            if (student == null) return (null, null);

            var attempt = await _repo.GetAttemptForUpdateAsync(attemptId);
            // Chỉ được xem/làm bài của chính mình
            if (attempt == null || attempt.StudentId != student.Id) return (student, null);

            return (student, attempt);
        }

        /// <summary>Chốt các lượt đang làm dở nhưng đã quá hạn (học sinh bỏ ngang, đóng trình duyệt...).</summary>
        private async Task FinalizeExpiredAttemptsAsync(int studentId, List<int> quizIds)
        {
            var now = Now;
            var stale = (await _repo.GetAttemptsAsync(studentId, quizIds))
                .Where(a => a.Status == QuizAttemptStatus.InProgress && now > a.ExpiresAt + SubmitGrace)
                .ToList();

            foreach (var item in stale)
            {
                var attempt = await _repo.GetAttemptForUpdateAsync(item.Id);
                if (attempt != null)
                    await GradeAsync(attempt, new Dictionary<int, int>(), QuizAttemptStatus.Expired);
            }
        }

        /// <summary>Chấm điểm tự động và lưu đáp án. attempt phải là entity đang được tracking.</summary>
        private async Task GradeAsync(QuizAttempt attempt, Dictionary<int, int> selected, QuizAttemptStatus finalStatus)
        {
            var questions = await _repo.GetQuestionsWithOptionsAsync(attempt.QuizId);
            var now = Now;
            decimal score = 0;
            var answerEntities = new List<QuizAnswer>();

            foreach (var (question, options) in questions)
            {
                QuizOption? chosen = null;
                if (selected.TryGetValue(question.Id, out int optionId))
                {
                    // Chỉ nhận đáp án thuộc đúng câu hỏi này
                    chosen = options.FirstOrDefault(o => o.Id == optionId);
                }

                bool isCorrect = chosen?.IsCorrect == true;
                decimal earned = isCorrect ? question.Points : 0;
                score += earned;

                answerEntities.Add(new QuizAnswer
                {
                    AttemptId = attempt.Id,
                    QuestionId = question.Id,
                    SelectedOptionId = chosen?.Id,
                    IsCorrect = isCorrect,
                    EarnedPoints = earned,
                    AnsweredAt = chosen != null ? now : null
                });
            }

            await _repo.AddAnswersAsync(answerEntities);

            attempt.Status = finalStatus;
            attempt.Score = score;
            attempt.SubmittedAt = finalStatus == QuizAttemptStatus.Expired ? null : now;
            attempt.GradedAt = now;

            await _repo.SaveChangesAsync();
        }
    }
}
