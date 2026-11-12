using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.Repositories.Teacher;
using EduCenterManagement.ViewModels.Teacher;

namespace EduCenterManagement.Services.Teacher
{
    public class TeacherService : ITeacherService
    {
        private readonly ITeacherRepository _repo;

        public TeacherService(ITeacherRepository repo)
        {
            _repo = repo;
        }

        public async Task<AssignedClassesViewModel?> GetAssignedClassesAsync(int userId)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return null;

            var classes = await _repo.GetAssignedClassesAsync(teacher.Id);
            var classIds = classes.Select(c => c.Id).ToList();

            var schedules = await _repo.GetClassSchedulesAsync(classIds);
            var enrollmentCounts = await _repo.GetEnrollmentCountsAsync(classIds);

            return new AssignedClassesViewModel
            {
                TeacherName = teacher.FullName,
                Classes = classes.Select(c =>
                {
                    var classSchedules = schedules
                        .Where(s => s.ClassId == c.Id)
                        .Select(s => $"{GetDayOfWeekName(s.DayOfWeek)} ({s.TimeSlot.StartTime:HH:mm} - {s.TimeSlot.EndTime:HH:mm})")
                        .ToList();

                    return new AssignedClassItemViewModel
                    {
                        ClassId = c.Id,
                        ClassName = c.Name,
                        SubjectName = c.Subject?.Name ?? "Chưa xác định",
                        StartDate = c.StartDate,
                        EndDate = c.EndDate,
                        Fee = c.Fee,
                        Status = c.Status,
                        CurrentStudents = enrollmentCounts.TryGetValue(c.Id, out int count) ? count : 0,
                        MaxStudents = c.MaxStudents,
                        Schedules = classSchedules
                    };
                }).ToList()
            };
        }

        public async Task<StudentListViewModel?> GetStudentListAsync(int userId, int classId)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return null;

            var classItem = await _repo.GetClassByIdAndTeacherAsync(classId, teacher.Id);
            if (classItem == null) return null;

            var enrollments = await _repo.GetConfirmedStudentsByClassIdAsync(classId);

            return new StudentListViewModel
            {
                ClassId = classItem.Id,
                ClassName = classItem.Name,
                SubjectName = classItem.Subject?.Name ?? "Chưa xác định",
                StartDate = classItem.StartDate,
                EndDate = classItem.EndDate,
                Students = enrollments.Select(e => new StudentItemViewModel
                {
                    StudentId = e.Student.Id,
                    StudentCode = $"HS{e.Student.Id:D3}",
                    FullName = e.Student.FullName,
                    DateOfBirth = e.Student.DateOfBirth
                }).ToList()
            };
        }

        public async Task<ClassGradebookViewModel?> GetClassGradebookAsync(int userId, int classId)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return null;

            var classItem = await _repo.GetClassByIdAndTeacherAsync(classId, teacher.Id);
            if (classItem == null) return null;

            var enrollments = await _repo.GetConfirmedStudentsByClassIdAsync(classId);
            var sessions = await _repo.GetSessionsByClassIdAsync(classId);
            var sessionIds = sessions.Select(s => s.Id).ToList();
            var takenSessions = sessions.Where(s => s.Status == ClassSessionStatus.Completed || s.SessionDate < DateOnly.FromDateTime(DateTime.Today)).ToList();
            var takenSessionIds = takenSessions.Select(s => s.Id).ToList();

            var attendances = await _repo.GetAttendanceRecordsByClassSessionsAsync(sessionIds);
            var quizzes = await _repo.GetQuizzesByClassIdAsync(classId);
            var quizIds = quizzes.Select(q => q.Id).ToList();
            var questions = await _repo.GetQuestionsByQuizIdsAsync(quizIds);
            var attempts = await _repo.GetQuizAttemptsByQuizIdsAsync(quizIds);

            var quizHeaders = quizzes.Select(q =>
            {
                var qQuestions = questions.Where(qq => qq.QuizId == q.Id).ToList();
                decimal totalPts = qQuestions.Sum(qq => qq.Points);
                return new GradebookQuizHeaderViewModel
                {
                    QuizId = q.Id,
                    Title = q.Title,
                    TotalPoints = totalPts
                };
            }).ToList();

            var studentRows = new List<StudentGradebookRowViewModel>();
            foreach (var e in enrollments)
            {
                var s = e.Student;
                var studentAttendances = attendances
                    .Where(a => a.StudentId == s.Id && takenSessionIds.Contains(a.SessionId))
                    .ToList();

                int present = studentAttendances.Count(a => a.Status == AttendanceStatus.Present);
                int absent = studentAttendances.Count(a => a.Status == AttendanceStatus.Absent);
                int takenCount = takenSessions.Count;
                double rate = takenCount > 0 ? Math.Round((double)present * 100.0 / takenCount, 1) : 100.0;

                var studentAttempts = attempts.Where(a => a.StudentId == s.Id).ToList();
                var quizScores = new Dictionary<int, decimal?>();
                var scores10 = new List<decimal>();

                foreach (var qh in quizHeaders)
                {
                    var myQuizAttempts = studentAttempts.Where(a => a.QuizId == qh.QuizId && a.Score.HasValue).ToList();
                    if (myQuizAttempts.Any())
                    {
                        decimal bestScore = myQuizAttempts.Max(a => a.Score!.Value);
                        quizScores[qh.QuizId] = bestScore;
                        if (qh.TotalPoints > 0)
                        {
                            scores10.Add(bestScore * 10m / qh.TotalPoints);
                        }
                    }
                    else
                    {
                        quizScores[qh.QuizId] = null;
                    }
                }

                decimal? avgScore10 = scores10.Any() ? Math.Round(scores10.Average(), 2) : null;

                studentRows.Add(new StudentGradebookRowViewModel
                {
                    StudentId = s.Id,
                    StudentCode = $"HS{s.Id:D3}",
                    FullName = s.FullName,
                    PresentCount = present,
                    AbsentCount = absent,
                    AttendanceRate = rate,
                    QuizScores = quizScores,
                    AverageQuizScore10 = avgScore10,
                    CompletedQuizzesCount = scores10.Count
                });
            }

            return new ClassGradebookViewModel
            {
                ClassId = classItem.Id,
                ClassName = classItem.Name,
                SubjectName = classItem.Subject?.Name ?? "Chưa xác định",
                TeacherName = teacher.FullName,
                StartDate = classItem.StartDate,
                EndDate = classItem.EndDate,
                TotalSessions = sessions.Count,
                TakenSessionsCount = takenSessions.Count,
                Quizzes = quizHeaders,
                Students = studentRows
            };
        }

        public async Task<TeacherTimetableViewModel?> GetTimetableAsync(int userId, int weekOffset)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return null;

            var today = DateOnly.FromDateTime(DateTime.Today);
            int diff = (7 + (int)today.DayOfWeek - (int)System.DayOfWeek.Monday) % 7;
            var currentMonday = today.AddDays(-diff);
            var weekStart = currentMonday.AddDays(weekOffset * 7);
            var weekEnd = weekStart.AddDays(6);

            var daysOfWeek = new List<DateOnly>();
            for (int i = 0; i < 7; i++)
            {
                daysOfWeek.Add(weekStart.AddDays(i));
            }

            var timeSlotEntities = await _repo.GetActiveTimeSlotsAsync();
            var timeSlots = timeSlotEntities.Select(ts => new TeacherTimeSlotHeaderViewModel
            {
                SlotId = ts.Id,
                Name = ts.Name,
                StartTime = ts.StartTime,
                EndTime = ts.EndTime
            }).ToList();

            var teacherClasses = await _repo.GetClassesInWeekRangeAsync(teacher.Id, weekStart, weekEnd);
            var classIds = teacherClasses.Select(c => c.Id).ToList();

            var enrollmentCounts = await _repo.GetEnrollmentCountsAsync(classIds);
            var sessionItems = new List<TeacherTimetableSessionViewModel>();

            if (classIds.Any())
            {
                // 1. Lấy buổi học cụ thể đã sinh
                var actualSessions = await _repo.GetActualSessionsInWeekRangeAsync(classIds, weekStart, weekEnd);

                foreach (var s in actualSessions)
                {
                    int matchedSlotId = s.ClassSchedule?.TimeSlotId ?? 0;
                    if (matchedSlotId == 0)
                    {
                        var foundSlot = timeSlots.FirstOrDefault(ts => ts.StartTime == s.StartsAt);
                        matchedSlotId = foundSlot?.SlotId ?? 0;
                    }

                    int count = enrollmentCounts.TryGetValue(s.ClassId, out int cnt) ? cnt : 0;

                    sessionItems.Add(new TeacherTimetableSessionViewModel
                    {
                        SessionId = s.Id,
                        ClassId = s.ClassId,
                        ClassName = s.Class.Name,
                        SubjectName = s.Class.Subject?.Name ?? "Chưa xác định",
                        SessionDate = s.SessionDate,
                        StartsAt = s.StartsAt,
                        EndsAt = s.EndsAt,
                        MatchedTimeSlotId = matchedSlotId,
                        CurrentStudents = count,
                        MaxStudents = s.Class.MaxStudents,
                        SessionStatus = s.Status,
                        IsActualSession = true
                    });
                }

                // 2. Dự phòng: Map từ lịch ClassSchedules nếu chưa sinh ClassSessions
                var schedules = await _repo.GetClassSchedulesAsync(classIds);
                foreach (var date in daysOfWeek)
                {
                    int dbDayOfWeek = date.DayOfWeek == System.DayOfWeek.Sunday ? 8 : (int)date.DayOfWeek + 1;

                    var matchingSchedules = schedules.Where(s =>
                        s.DayOfWeek == dbDayOfWeek &&
                        s.Class.StartDate <= date &&
                        s.Class.EndDate >= date).ToList();

                    foreach (var sch in matchingSchedules)
                    {
                        bool alreadyHasActual = actualSessions.Any(a =>
                            a.ClassId == sch.ClassId &&
                            a.SessionDate == date);

                        if (!alreadyHasActual)
                        {
                            int count = enrollmentCounts.TryGetValue(sch.ClassId, out int cnt) ? cnt : 0;

                            sessionItems.Add(new TeacherTimetableSessionViewModel
                            {
                                SessionId = null,
                                ClassId = sch.ClassId,
                                ClassName = sch.Class.Name,
                                SubjectName = sch.Class.Subject?.Name ?? "Chưa xác định",
                                SessionDate = date,
                                StartsAt = sch.TimeSlot.StartTime,
                                EndsAt = sch.TimeSlot.EndTime,
                                MatchedTimeSlotId = sch.TimeSlotId,
                                CurrentStudents = count,
                                MaxStudents = sch.Class.MaxStudents,
                                SessionStatus = null,
                                IsActualSession = false
                            });
                        }
                    }
                }
            }

            return new TeacherTimetableViewModel
            {
                TeacherName = teacher.FullName,
                WeekStartDate = weekStart,
                WeekEndDate = weekEnd,
                CurrentWeekOffset = weekOffset,
                DaysOfWeek = daysOfWeek,
                TimeSlots = timeSlots,
                Sessions = sessionItems
            };
        }

        public async Task<ClassSessionsViewModel?> GetSessionsAsync(int userId, int classId, string filter)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return null;

            var classItem = await _repo.GetClassByIdAndTeacherAsync(classId, teacher.Id);
            if (classItem == null) return null;

            var rawSessions = await _repo.GetSessionsByClassIdAsync(classId);

            var today = DateOnly.FromDateTime(DateTime.Today);
            var nowTime = TimeOnly.FromDateTime(DateTime.Now);

            var allSessionVMs = rawSessions.Select(s => new ClassSessionItemViewModel
            {
                SessionId = s.Id,
                SessionNumber = s.SessionNumber,
                SessionDate = s.SessionDate,
                DayOfWeekName = GetDayOfWeekName(s.SessionDate.DayOfWeek == System.DayOfWeek.Sunday ? 8 : (int)s.SessionDate.DayOfWeek + 1),
                StartsAt = s.StartsAt,
                EndsAt = s.EndsAt,
                Status = s.Status,
                IsToday = s.SessionDate == today,
                IsPast = s.SessionDate < today || (s.SessionDate == today && s.EndsAt <= nowTime)
            }).ToList();

            int total = allSessionVMs.Count;
            int completed = allSessionVMs.Count(s => s.Status == ClassSessionStatus.Completed);
            int scheduled = allSessionVMs.Count(s => s.Status == ClassSessionStatus.Scheduled);
            int cancelled = allSessionVMs.Count(s => s.Status == ClassSessionStatus.Cancelled);
            var todaySession = allSessionVMs.FirstOrDefault(s => s.IsToday);

            var filteredSessions = filter?.ToLower() switch
            {
                "today" => allSessionVMs.Where(s => s.IsToday).ToList(),
                "scheduled" => allSessionVMs.Where(s => s.Status == ClassSessionStatus.Scheduled).ToList(),
                "completed" => allSessionVMs.Where(s => s.Status == ClassSessionStatus.Completed).ToList(),
                "cancelled" => allSessionVMs.Where(s => s.Status == ClassSessionStatus.Cancelled).ToList(),
                _ => allSessionVMs
            };

            return new ClassSessionsViewModel
            {
                ClassId = classItem.Id,
                ClassName = classItem.Name,
                SubjectName = classItem.Subject?.Name ?? "Chưa xác định",
                StartDate = classItem.StartDate,
                EndDate = classItem.EndDate,
                TeacherName = teacher.FullName,
                TotalSessions = total,
                CompletedSessions = completed,
                ScheduledSessions = scheduled,
                CancelledSessions = cancelled,
                TodaySession = todaySession,
                CurrentFilter = filter?.ToLower() ?? "all",
                Sessions = filteredSessions
            };
        }

        public async Task<(bool Success, string Message, int SessionNumber)> UpdateSessionStatusAsync(int userId, int sessionId, int classId, ClassSessionStatus status)
        {
            var teacher = await _repo.GetTeacherProfileByUserIdAsync(userId);
            if (teacher == null) return (false, "Không tìm thấy hồ sơ giáo viên.", 0);

            var session = await _repo.GetSessionByIdAndTeacherAsync(sessionId, teacher.Id);
            if (session == null) return (false, "Không tìm thấy buổi học hoặc bạn không có quyền cập nhật.", 0);
            if (session.Class.Status != ClassStatus.InProgress)
                return (false, "Chỉ được cập nhật buổi học khi lớp đang ở trạng thái Đang học.", 0);

            if (status == ClassSessionStatus.Completed)
                return (false, "Không thể chuyển trạng thái hoàn thành thủ công. Buổi học sẽ tự động hoàn thành khi bạn lưu điểm danh.", 0);

            if (status == ClassSessionStatus.Cancelled)
                return (false, "Giáo viên không có quyền hủy buổi học. Vui lòng liên hệ Quản trị viên.", 0);

            bool updated = await _repo.UpdateSessionStatusAsync(sessionId, teacher.Id, status);
            if (!updated) return (false, "Cập nhật trạng thái thất bại.", 0);

            string msg = status switch
            {
                ClassSessionStatus.Completed => $"Đã xác nhận hoàn thành Buổi {session.SessionNumber}.",
                ClassSessionStatus.Cancelled => $"Đã chuyển Buổi {session.SessionNumber} sang trạng thái Báo nghỉ / Hủy.",
                ClassSessionStatus.Scheduled => $"Đã khôi phục Buổi {session.SessionNumber} về trạng thái Đã lên lịch.",
                _ => $"Đã cập nhật trạng thái Buổi {session.SessionNumber}."
            };

            return (true, msg, session.SessionNumber);
        }

        private static string GetDayOfWeekName(int day) => day switch
        {
            2 => "Thứ Hai",
            3 => "Thứ Ba",
            4 => "Thứ Tư",
            5 => "Thứ Năm",
            6 => "Thứ Sáu",
            7 => "Thứ Bảy",
            8 => "Chủ Nhật",
            _ => $"Thứ {day}"
        };
    }
}

