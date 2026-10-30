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

            // Tự động sinh buổi học nếu chưa có
            await AutoGenerateSessionsIfNeededAsync(classItem);

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

        private async Task AutoGenerateSessionsIfNeededAsync(Class classItem)
        {
            bool hasSessions = await _repo.HasSessionsAsync(classItem.Id);
            if (hasSessions) return;

            var schedules = await _repo.GetSchedulesByClassIdAsync(classItem.Id);
            if (!schedules.Any() || classItem.StartDate > classItem.EndDate) return;

            var newSessions = new List<ClassSession>();
            int sessionNumber = 1;
            var now = DateTime.UtcNow;

            for (var date = classItem.StartDate; date <= classItem.EndDate; date = date.AddDays(1))
            {
                int dbDayOfWeek = date.DayOfWeek == System.DayOfWeek.Sunday ? 8 : (int)date.DayOfWeek + 1;

                var matchingSchedules = schedules
                    .Where(s => s.DayOfWeek == dbDayOfWeek)
                    .OrderBy(s => s.TimeSlot.StartTime)
                    .ToList();

                foreach (var sch in matchingSchedules)
                {
                    newSessions.Add(new ClassSession
                    {
                        ClassId = classItem.Id,
                        ClassScheduleId = sch.Id,
                        SessionDate = date,
                        StartsAt = sch.TimeSlot.StartTime,
                        EndsAt = sch.TimeSlot.EndTime,
                        SessionNumber = sessionNumber++,
                        Status = ClassSessionStatus.Scheduled,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }

            if (newSessions.Any())
            {
                await _repo.AddSessionsRangeAsync(newSessions);
            }
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

