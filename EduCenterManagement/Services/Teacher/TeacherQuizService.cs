using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.Repositories.Teacher;
using EduCenterManagement.ViewModels.Teacher.Quiz;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduCenterManagement.Services.Teacher
{
    public class TeacherQuizService : ITeacherQuizService
    {
        private readonly ITeacherQuizRepository _repo;

        public TeacherQuizService(ITeacherQuizRepository repo)
        {
            _repo = repo;
        }

        public async Task<TeacherQuizListViewModel?> GetQuizListAsync(int userId, string filter, int? subjectId, string? search)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return null;

            var subjects = await _repo.GetAllSubjectsAsync();
            var subjectItems = subjects.Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = s.Name,
                Selected = subjectId.HasValue && subjectId.Value == s.Id
            }).ToList();

            var (rawQuizzes, totalMy, totalAll, totalOthers) = await _repo.GetQuizzesFilteredAsync(
                teacher.Id, filter, subjectId, search);

            var quizIds = rawQuizzes.Select(q => q.Id).ToList();
            var questionStats = await _repo.GetQuestionStatsByQuizIdsAsync(quizIds);
            var attemptCounts = await _repo.GetAttemptCountsByQuizIdsAsync(quizIds);

            var quizItems = rawQuizzes.Select(q =>
            {
                questionStats.TryGetValue(q.Id, out var qStat);
                attemptCounts.TryGetValue(q.Id, out int attempts);

                return new QuizListItemViewModel
                {
                    Id = q.Id,
                    Title = q.Title,
                    SubjectName = q.Class?.Subject?.Name ?? "Chưa rõ",
                    ClassId = q.ClassId,
                    ClassName = q.Class?.Name ?? "Chưa rõ",
                    SessionId = q.SessionId,
                    SessionNumber = q.Session?.SessionNumber,
                    SessionDate = q.Session?.SessionDate,
                    CreatedByTeacherId = q.CreatedByTeacherId,
                    CreatedByTeacherName = q.CreatedByTeacher?.FullName ?? "Giáo viên",
                    IsMine = q.CreatedByTeacherId == teacher.Id,
                    DurationMinutes = q.DurationMinutes,
                    AvailableFrom = q.AvailableFrom,
                    AvailableUntil = q.AvailableUntil,
                    MaxAttempts = q.MaxAttempts,
                    Status = q.Status,
                    QuestionCount = qStat.Count,
                    TotalPoints = qStat.TotalPoints,
                    SubmittedAttemptsCount = attempts
                };
            }).ToList();

            return new TeacherQuizListViewModel
            {
                CurrentFilter = filter?.ToLower() ?? "my",
                SelectedSubjectId = subjectId,
                SearchQuery = search,
                CurrentTeacherId = teacher.Id,
                CurrentTeacherName = teacher.FullName,
                TotalMyQuizzes = totalMy,
                TotalAllQuizzes = totalAll,
                TotalOtherQuizzes = totalOthers,
                Subjects = subjectItems,
                Quizzes = quizItems
            };
        }

        public async Task<CreateQuizViewModel?> GetCreateQuizViewModelAsync(int userId, int? sessionId, int? classId)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return null;

            var classes = await _repo.GetTeacherClassesAsync(teacher.Id);

            var model = new CreateQuizViewModel
            {
                AvailableFrom = DateTime.Now,
                AvailableUntil = DateTime.Now.AddDays(7),
                DurationMinutes = 15,
                MaxAttempts = 1,
                Status = QuizStatus.Draft,
                Classes = classes.Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                }).ToList()
            };

            if (sessionId.HasValue && sessionId.Value > 0)
            {
                var session = await _repo.GetSessionByIdAndTeacherAsync(sessionId.Value, teacher.Id);
                if (session != null)
                {
                    model.ClassId = session.ClassId;
                    model.SessionId = session.Id;
                    model.Title = $"Quiz ôn tập - {session.Class.Name} - Buổi {session.SessionNumber}";
                }
            }
            else if (classId.HasValue && classId.Value > 0)
            {
                model.ClassId = classId.Value;
            }
            else if (classes.Any())
            {
                model.ClassId = classes.First().Id;
            }

            if (model.ClassId > 0)
            {
                var sessions = await _repo.GetSessionsByClassIdAsync(model.ClassId, teacher.Id);
                var sessionList = new List<SelectListItem>
                {
                    new SelectListItem { Value = "", Text = "-- Không gắn vào buổi học (Quiz chung của cả lớp) --" }
                };

                sessionList.AddRange(sessions.Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = $"Buổi {s.SessionNumber} ({s.SessionDate:dd/MM/yyyy} - {s.StartsAt:HH:mm} đến {s.EndsAt:HH:mm})",
                    Selected = s.Id == model.SessionId
                }));

                model.Sessions = sessionList;
            }

            return model;
        }

        public async Task<(bool Success, string Message, int QuizId)> CreateQuizAsync(int userId, CreateQuizViewModel model)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return (false, "Không tìm thấy hồ sơ giáo viên.", 0);

            if (model.AvailableUntil <= model.AvailableFrom)
            {
                return (false, "Thời gian kết thúc phải lớn hơn thời gian bắt đầu.", 0);
            }

            var classItem = await _repo.GetClassByIdAndTeacherAsync(model.ClassId, teacher.Id);
            if (classItem == null)
            {
                return (false, "Lớp học không tồn tại hoặc bạn không phụ trách lớp này.", 0);
            }

            if (model.SessionId.HasValue && model.SessionId.Value > 0)
            {
                var session = await _repo.GetSessionByIdAndTeacherAsync(model.SessionId.Value, teacher.Id);
                if (session == null || session.ClassId != model.ClassId)
                {
                    return (false, "Buổi học không hợp lệ hoặc không thuộc lớp đã chọn.", 0);
                }
            }
            else
            {
                model.SessionId = null;
            }

            var now = DateTime.UtcNow;
            var newQuiz = new Quiz
            {
                ClassId = model.ClassId,
                SessionId = model.SessionId,
                CreatedByTeacherId = teacher.Id,
                Title = model.Title.Trim(),
                DurationMinutes = model.DurationMinutes,
                AvailableFrom = model.AvailableFrom,
                AvailableUntil = model.AvailableUntil,
                MaxAttempts = model.MaxAttempts,
                Status = model.Status,
                CreatedAt = now,
                UpdatedAt = now
            };

            var created = await _repo.AddQuizAsync(newQuiz);
            return (true, $"Đã tạo bài Quiz '{created.Title}' thành công! Hãy thêm các câu hỏi trắc nghiệm dưới đây.", created.Id);
        }

        public async Task<QuizDetailViewModel?> GetQuizDetailAsync(int userId, int quizId, string tab)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return null;

            var quiz = await _repo.GetQuizWithDetailsByIdAsync(quizId);
            if (quiz == null) return null;

            bool isMine = quiz.CreatedByTeacherId == teacher.Id;

            var questions = await _repo.GetQuestionsByQuizIdAsync(quizId);
            var questionIds = questions.Select(q => q.Id).ToList();
            var options = await _repo.GetOptionsByQuestionIdsAsync(questionIds);

            var questionVMs = questions.Select(q => new QuizQuestionDetailViewModel
            {
                Id = q.Id,
                SortOrder = q.SortOrder,
                QuestionText = q.QuestionText,
                Points = q.Points,
                Options = options
                    .Where(o => o.QuestionId == q.Id)
                    .Select(o => new QuizOptionDetailViewModel
                    {
                        Id = o.Id,
                        SortOrder = o.SortOrder,
                        OptionText = o.OptionText,
                        IsCorrect = o.IsCorrect
                    }).ToList()
            }).ToList();

            var attemptVMs = new List<QuizAttemptDetailViewModel>();
            if (isMine)
            {
                var attempts = await _repo.GetAttemptsByQuizIdAsync(quizId);
                decimal totalPoints = questionVMs.Sum(q => q.Points);

                attemptVMs = attempts.Select(a => new QuizAttemptDetailViewModel
                {
                    AttemptId = a.Id,
                    StudentId = a.StudentId,
                    StudentCode = $"HS{a.Student.Id:D3}",
                    StudentFullName = a.Student.FullName,
                    AttemptNumber = a.AttemptNumber,
                    StartedAt = a.StartedAt,
                    SubmittedAt = a.SubmittedAt,
                    Status = a.Status,
                    Score = a.Score,
                    MaxScore = totalPoints
                }).ToList();
            }

            return new QuizDetailViewModel
            {
                Id = quiz.Id,
                Title = quiz.Title,
                ClassId = quiz.ClassId,
                ClassName = quiz.Class?.Name ?? "Chưa rõ",
                SubjectName = quiz.Class?.Subject?.Name ?? "Chưa rõ",
                SessionId = quiz.SessionId,
                SessionNumber = quiz.Session?.SessionNumber,
                SessionDate = quiz.Session?.SessionDate,
                CreatedByTeacherId = quiz.CreatedByTeacherId,
                CreatedByTeacherName = quiz.CreatedByTeacher?.FullName ?? "Giáo viên",
                IsMine = isMine,
                DurationMinutes = quiz.DurationMinutes,
                AvailableFrom = quiz.AvailableFrom,
                AvailableUntil = quiz.AvailableUntil,
                MaxAttempts = quiz.MaxAttempts,
                Status = quiz.Status,
                TotalPoints = questionVMs.Sum(q => q.Points),
                ActiveTab = tab?.ToLower() == "results" ? "results" : "questions",
                Questions = questionVMs,
                Attempts = attemptVMs
            };
        }

        public async Task<(bool Success, string Message)> AddQuestionAsync(int userId, QuizQuestionFormViewModel model)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return (false, "Không tìm thấy hồ sơ giáo viên.");

            var quiz = await _repo.GetQuizByIdAsync(model.QuizId);
            if (quiz == null || quiz.CreatedByTeacherId != teacher.Id)
            {
                return (false, "Bài Quiz không tồn tại hoặc bạn không có quyền sửa đề thi này.");
            }

            if (string.IsNullOrWhiteSpace(model.QuestionText))
            {
                return (false, "Nội dung câu hỏi không được để trống.");
            }

            var filledOptions = model.Options?.Where(o => !string.IsNullOrWhiteSpace(o.OptionText)).ToList() ?? new();
            if (filledOptions.Count < 2)
            {
                return (false, "Vui lòng nhập ít nhất 2 phương án đáp án.");
            }

            var currentQuestions = await _repo.GetQuestionsByQuizIdAsync(model.QuizId);
            int nextSort = (currentQuestions.Any() ? currentQuestions.Max(q => q.SortOrder) : 0) + 1;

            var now = DateTime.UtcNow;
            var question = new QuizQuestion
            {
                QuizId = model.QuizId,
                QuestionText = model.QuestionText.Trim(),
                Points = model.Points <= 0 ? 1.00m : model.Points,
                SortOrder = nextSort,
                CreatedAt = now,
                UpdatedAt = now
            };

            var options = new List<QuizOption>();
            for (int i = 0; i < model.Options!.Count; i++)
            {
                var opt = model.Options[i];
                if (string.IsNullOrWhiteSpace(opt.OptionText)) continue;

                options.Add(new QuizOption
                {
                    OptionText = opt.OptionText.Trim(),
                    IsCorrect = (i == model.CorrectOptionIndex),
                    SortOrder = i + 1
                });
            }

            await _repo.AddQuestionWithOptionsAsync(question, options);
            return (true, $"Đã thêm câu hỏi số {nextSort} thành công!");
        }

        public async Task<(bool Success, string Message)> DeleteQuestionAsync(int userId, int questionId, int quizId)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return (false, "Không tìm thấy hồ sơ giáo viên.");

            bool deleted = await _repo.DeleteQuestionAsync(questionId, quizId, teacher.Id);
            if (!deleted) return (false, "Không tìm thấy câu hỏi hoặc bạn không có quyền xóa.");

            return (true, "Đã xóa câu hỏi thành công.");
        }

        public async Task<CloneQuizViewModel?> GetCloneViewModelAsync(int userId, int sourceQuizId)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return null;

            var sourceQuiz = await _repo.GetQuizWithDetailsByIdAsync(sourceQuizId);
            if (sourceQuiz == null) return null;

            var questions = await _repo.GetQuestionsByQuizIdAsync(sourceQuizId);
            var myClasses = await _repo.GetTeacherClassesAsync(teacher.Id);

            var model = new CloneQuizViewModel
            {
                SourceQuizId = sourceQuiz.Id,
                SourceQuizTitle = sourceQuiz.Title,
                SourceTeacherName = sourceQuiz.CreatedByTeacher?.FullName ?? "Đồng nghiệp",
                SourceSubjectName = sourceQuiz.Class?.Subject?.Name ?? "Chưa rõ",
                QuestionCount = questions.Count,
                NewTitle = $"Bản sao - {sourceQuiz.Title}",
                DurationMinutes = sourceQuiz.DurationMinutes,
                AvailableFrom = DateTime.Now,
                AvailableUntil = DateTime.Now.AddDays(7),
                TargetClasses = myClasses.Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                }).ToList()
            };

            if (myClasses.Any())
            {
                model.TargetClassId = myClasses.First().Id;
                var sessions = await _repo.GetSessionsByClassIdAsync(model.TargetClassId, teacher.Id);
                var sessionList = new List<SelectListItem>
                {
                    new SelectListItem { Value = "", Text = "-- Không gắn vào buổi học (Quiz chung của cả lớp) --" }
                };

                sessionList.AddRange(sessions.Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = $"Buổi {s.SessionNumber} ({s.SessionDate:dd/MM/yyyy} - {s.StartsAt:HH:mm} đến {s.EndsAt:HH:mm})",
                    Selected = s.Id == model.TargetSessionId
                }));

                model.TargetSessions = sessionList;
            }

            return model;
        }

        public async Task<(bool Success, string Message, int NewQuizId)> CloneQuizAsync(int userId, CloneQuizViewModel model)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return (false, "Không tìm thấy hồ sơ giáo viên.", 0);

            var sourceQuiz = await _repo.GetQuizByIdAsync(model.SourceQuizId);
            if (sourceQuiz == null) return (false, "Không tìm thấy bài Quiz nguồn.", 0);

            var targetClass = await _repo.GetClassByIdAndTeacherAsync(model.TargetClassId, teacher.Id);
            if (targetClass == null) return (false, "Lớp học mục tiêu không hợp lệ.", 0);

            if (model.TargetSessionId.HasValue && model.TargetSessionId.Value > 0)
            {
                var targetSession = await _repo.GetSessionByIdAndTeacherAsync(model.TargetSessionId.Value, teacher.Id);
                if (targetSession == null || targetSession.ClassId != model.TargetClassId)
                {
                    return (false, "Buổi học không hợp lệ hoặc không thuộc lớp mục tiêu.", 0);
                }
            }
            else
            {
                model.TargetSessionId = null;
            }

            if (model.AvailableUntil <= model.AvailableFrom)
            {
                return (false, "Thời gian kết thúc phải lớn hơn thời gian bắt đầu.", 0);
            }

            var now = DateTime.UtcNow;
            var newQuiz = new Quiz
            {
                ClassId = model.TargetClassId,
                SessionId = model.TargetSessionId,
                CreatedByTeacherId = teacher.Id,
                Title = model.NewTitle.Trim(),
                DurationMinutes = model.DurationMinutes,
                AvailableFrom = model.AvailableFrom,
                AvailableUntil = model.AvailableUntil,
                MaxAttempts = sourceQuiz.MaxAttempts,
                Status = QuizStatus.Draft,
                CreatedAt = now,
                UpdatedAt = now
            };

            var createdQuiz = await _repo.CloneQuizAsync(sourceQuiz, newQuiz);
            return (true, "Đã nhân bản bài Quiz thành công! Bạn có thể xem lại và chỉnh sửa.", createdQuiz.Id);
        }

        public async Task<(bool Success, string Message)> UpdateStatusAsync(int userId, int quizId, QuizStatus status)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return (false, "Không tìm thấy hồ sơ giáo viên.");

            bool updated = await _repo.UpdateQuizStatusAsync(quizId, teacher.Id, status);
            if (!updated) return (false, "Không có quyền cập nhật bài Quiz này.");

            string msg = status switch
            {
                QuizStatus.Published => "Đã mở xuất bản bài Quiz cho học sinh làm bài.",
                QuizStatus.Closed => "Đã đóng bài Quiz.",
                _ => "Đã chuyển bài Quiz về trạng thái Bản nháp."
            };

            return (true, msg);
        }

        public async Task<(bool Success, string Message)> DeleteQuizAsync(int userId, int quizId)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return (false, "Không tìm thấy hồ sơ giáo viên.");

            bool deleted = await _repo.DeleteQuizCascadeAsync(quizId, teacher.Id);
            if (!deleted) return (false, "Bài Quiz không tồn tại hoặc bạn không có quyền xóa.");

            return (true, "Đã xóa bài Quiz thành công.");
        }

        public async Task<StudentAttemptDetailViewModel?> GetStudentAttemptDetailAsync(int userId, int attemptId)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return null;

            var attempt = await _repo.GetStudentAttemptWithDetailsAsync(attemptId);
            if (attempt == null || attempt.Quiz.CreatedByTeacherId != teacher.Id)
            {
                return null;
            }

            var questions = await _repo.GetQuestionsByQuizIdAsync(attempt.QuizId);
            var qIds = questions.Select(q => q.Id).ToList();
            var options = await _repo.GetOptionsByQuestionIdsAsync(qIds);
            var answers = await _repo.GetAnswersByAttemptIdAsync(attemptId);

            var answerVMs = questions.Select(q =>
            {
                var ans = answers.FirstOrDefault(a => a.QuestionId == q.Id);
                var qOptions = options.Where(o => o.QuestionId == q.Id).OrderBy(o => o.SortOrder).ToList();

                return new StudentAnswerItemViewModel
                {
                    QuestionId = q.Id,
                    SortOrder = q.SortOrder,
                    QuestionText = q.QuestionText,
                    Points = q.Points,
                    EarnedPoints = ans?.EarnedPoints ?? 0,
                    IsCorrect = ans?.IsCorrect,
                    Options = qOptions.Select(o => new StudentAnswerOptionViewModel
                    {
                        OptionId = o.Id,
                        OptionText = o.OptionText,
                        IsCorrectOption = o.IsCorrect,
                        IsSelectedByStudent = ans != null && ans.SelectedOptionId == o.Id
                    }).ToList()
                };
            }).ToList();

            return new StudentAttemptDetailViewModel
            {
                AttemptId = attempt.Id,
                QuizId = attempt.QuizId,
                QuizTitle = attempt.Quiz.Title,
                StudentFullName = attempt.Student.FullName,
                StudentCode = $"HS{attempt.Student.Id:D3}",
                AttemptNumber = attempt.AttemptNumber,
                StartedAt = attempt.StartedAt,
                SubmittedAt = attempt.SubmittedAt,
                Status = attempt.Status,
                Score = attempt.Score,
                TotalPoints = questions.Sum(q => q.Points),
                Answers = answerVMs
            };
        }

        public async Task<List<object>> GetSessionsByClassAsync(int userId, int classId)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return new List<object>();

            var sessions = await _repo.GetSessionsByClassIdAsync(classId, teacher.Id);
            var result = new List<object>
            {
                new { id = "", text = "-- Không gắn vào buổi học (Quiz chung của cả lớp) --" }
            };

            result.AddRange(sessions.Select(s => (object)new
            {
                id = s.Id.ToString(),
                text = $"Buổi {s.SessionNumber} ({s.SessionDate:dd/MM/yyyy} - {s.StartsAt:HH:mm} đến {s.EndsAt:HH:mm})"
            }));

            return result;
        }
    }
}
