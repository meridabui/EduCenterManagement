using System.Security.Claims;
using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.ViewModels.Teacher;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Controllers.Teacher
{
    [Authorize(Roles = "Teacher")]
    [Route("Teacher")]
    public class TeacherController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TeacherController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Teacher hoặc /Teacher/Index -> Chuyển về màn hình danh sách lớp
        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index()
        {
            return RedirectToAction(nameof(AssignedClasses));
        }

        // GET: /Teacher/AssignedClasses
        [HttpGet("AssignedClasses")]
        public async Task<IActionResult> AssignedClasses()
        {
            var teacher = await GetCurrentTeacherProfileAsync();
            if (teacher == null)
            {
                return Forbid();
            }

            // Lấy các lớp được phân công cho Giáo viên (loại trừ các lớp đã bị Hủy)
            var classes = await _context.Classes
                .AsNoTracking()
                .Where(c => c.TeacherId == teacher.Id && c.Status != ClassStatus.Cancelled)
                .Include(c => c.Subject)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var classIds = classes.Select(c => c.Id).ToList();

            // Lấy lịch học trong tuần theo từng lớp
            var schedules = await _context.ClassSchedules
                .AsNoTracking()
                .Where(s => classIds.Contains(s.ClassId))
                .Include(s => s.TimeSlot)
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.TimeSlot.StartTime)
                .ToListAsync();

            // Đếm số lượng học sinh đã đăng ký (Confirmed hoặc PendingPayment)
            var enrollmentCounts = await _context.Enrollments
                .AsNoTracking()
                .Where(e => classIds.Contains(e.ClassId) &&
                           (e.Status == EnrollmentStatus.Confirmed || e.Status == EnrollmentStatus.PendingPayment))
                .GroupBy(e => e.ClassId)
                .Select(g => new { ClassId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ClassId, x => x.Count);

            var model = new AssignedClassesViewModel
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

            return View("~/Views/Teacher/AssignedClasses.cshtml", model);
        }

        // GET: /Teacher/StudentList/{classId}
        [HttpGet("StudentList/{classId:int}")]
        public async Task<IActionResult> StudentList(int classId)
        {
            var teacher = await GetCurrentTeacherProfileAsync();
            if (teacher == null)
            {
                return Forbid();
            }

            // Kiểm tra lớp có đúng do giáo viên đang đăng nhập phụ trách không
            var classItem = await _context.Classes
                .AsNoTracking()
                .Include(c => c.Subject)
                .FirstOrDefaultAsync(c => c.Id == classId && c.TeacherId == teacher.Id);

            if (classItem == null)
            {
                return NotFound("Lớp học không tồn tại hoặc bạn không được phân công phụ trách lớp này.");
            }

            // Lấy danh sách học sinh đã đăng ký vào lớp (Status = Confirmed hoặc PendingPayment)
            var students = await _context.Enrollments
                .AsNoTracking()
                .Where(e => e.ClassId == classId &&
                           (e.Status == EnrollmentStatus.Confirmed || e.Status == EnrollmentStatus.PendingPayment))
                .Include(e => e.Student)
                .OrderBy(e => e.Student.FullName)
                .Select(e => new StudentItemViewModel
                {
                    StudentCode = $"HS{e.Student.Id:D3}",
                    FullName = e.Student.FullName,
                    DateOfBirth = e.Student.DateOfBirth
                })
                .ToListAsync();

            var model = new StudentListViewModel
            {
                ClassId = classItem.Id,
                ClassName = classItem.Name,
                SubjectName = classItem.Subject?.Name ?? "Chưa xác định",
                StartDate = classItem.StartDate,
                EndDate = classItem.EndDate,
                Students = students
            };

            return View("~/Views/Teacher/StudentList.cshtml", model);
        }

        // GET: /Teacher/Timetable
        [HttpGet("Timetable")]
        public async Task<IActionResult> Timetable(int weekOffset = 0)
        {
            var teacher = await GetCurrentTeacherProfileAsync();
            if (teacher == null)
            {
                return Forbid();
            }

            // Tính ngày Thứ 2 của tuần hiện tại hoặc theo weekOffset
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

            // Lấy danh sách các ca học (TimeSlots) đang Active
            var timeSlotEntities = await _context.TimeSlots
                .AsNoTracking()
                .Where(t => t.Status == TimeSlotStatus.Active)
                .OrderBy(t => t.StartTime)
                .ToListAsync();

            var timeSlots = timeSlotEntities.Select(ts => new TeacherTimeSlotHeaderViewModel
            {
                SlotId = ts.Id,
                Name = ts.Name,
                StartTime = ts.StartTime,
                EndTime = ts.EndTime
            }).ToList();

            // Lấy các lớp do giáo viên này phụ trách và còn trong thời gian hoạt động
            var teacherClasses = await _context.Classes
                .AsNoTracking()
                .Where(c => c.TeacherId == teacher.Id &&
                            c.Status != ClassStatus.Cancelled &&
                            c.StartDate <= weekEnd &&
                            c.EndDate >= weekStart)
                .Include(c => c.Subject)
                .ToListAsync();

            var classIds = teacherClasses.Select(c => c.Id).ToList();

            // Đếm số lượng học sinh của từng lớp
            var enrollmentCounts = await _context.Enrollments
                .AsNoTracking()
                .Where(e => classIds.Contains(e.ClassId) &&
                           (e.Status == EnrollmentStatus.Confirmed || e.Status == EnrollmentStatus.PendingPayment))
                .GroupBy(e => e.ClassId)
                .Select(g => new { ClassId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ClassId, x => x.Count);

            var sessionItems = new List<TeacherTimetableSessionViewModel>();

            if (classIds.Any())
            {
                // 1. Kiểm tra nếu đã có buổi học cụ thể trong class_sessions
                var actualSessions = await _context.ClassSessions
                    .AsNoTracking()
                    .Where(s => classIds.Contains(s.ClassId) &&
                                s.SessionDate >= weekStart &&
                                s.SessionDate <= weekEnd)
                    .Include(s => s.Class)
                        .ThenInclude(c => c.Subject)
                    .Include(s => s.ClassSchedule)
                    .ToListAsync();

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

                // 2. Dự phòng: Nếu chưa sinh class_sessions, tự động map từ class_schedules
                var schedules = await _context.ClassSchedules
                    .AsNoTracking()
                    .Where(s => classIds.Contains(s.ClassId))
                    .Include(s => s.TimeSlot)
                    .Include(s => s.Class)
                        .ThenInclude(c => c.Subject)
                    .ToListAsync();

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

            var model = new TeacherTimetableViewModel
            {
                TeacherName = teacher.FullName,
                WeekStartDate = weekStart,
                WeekEndDate = weekEnd,
                CurrentWeekOffset = weekOffset,
                DaysOfWeek = daysOfWeek,
                TimeSlots = timeSlots,
                Sessions = sessionItems
            };

            return View("~/Views/Teacher/Timetable.cshtml", model);
        }

        // GET: /Teacher/Sessions/{classId}
        [HttpGet("Sessions/{classId:int}")]
        public async Task<IActionResult> Sessions(int classId, string filter = "all")
        {
            var teacher = await GetCurrentTeacherProfileAsync();
            if (teacher == null)
            {
                return Forbid();
            }

            var classItem = await _context.Classes
                .Include(c => c.Subject)
                .FirstOrDefaultAsync(c => c.Id == classId && c.TeacherId == teacher.Id);

            if (classItem == null)
            {
                return NotFound("Lớp học không tồn tại hoặc bạn không được phân công phụ trách lớp này.");
            }

            // Tự động sinh buổi học nếu lớp chưa có
            await AutoGenerateSessionsIfNeededAsync(classItem);

            // Lấy toàn bộ buổi học của lớp
            var rawSessions = await _context.ClassSessions
                .AsNoTracking()
                .Where(s => s.ClassId == classId)
                .OrderBy(s => s.SessionNumber)
                .ToListAsync();

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

            // Thống kê
            int total = allSessionVMs.Count;
            int completed = allSessionVMs.Count(s => s.Status == ClassSessionStatus.Completed);
            int scheduled = allSessionVMs.Count(s => s.Status == ClassSessionStatus.Scheduled);
            int cancelled = allSessionVMs.Count(s => s.Status == ClassSessionStatus.Cancelled);
            var todaySession = allSessionVMs.FirstOrDefault(s => s.IsToday);

            // Áp dụng bộ lọc
            var filteredSessions = filter?.ToLower() switch
            {
                "today" => allSessionVMs.Where(s => s.IsToday).ToList(),
                "scheduled" => allSessionVMs.Where(s => s.Status == ClassSessionStatus.Scheduled).ToList(),
                "completed" => allSessionVMs.Where(s => s.Status == ClassSessionStatus.Completed).ToList(),
                "cancelled" => allSessionVMs.Where(s => s.Status == ClassSessionStatus.Cancelled).ToList(),
                _ => allSessionVMs
            };

            var model = new ClassSessionsViewModel
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

            return View("~/Views/Teacher/Sessions.cshtml", model);
        }

        // POST: /Teacher/UpdateSessionStatus
        [HttpPost("UpdateSessionStatus")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSessionStatus(int sessionId, ClassSessionStatus status, int classId, string filter = "all")
        {
            var teacher = await GetCurrentTeacherProfileAsync();
            if (teacher == null)
            {
                return Forbid();
            }

            var session = await _context.ClassSessions
                .Include(s => s.Class)
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.Class.TeacherId == teacher.Id);

            if (session == null)
            {
                return NotFound("Không tìm thấy buổi học hoặc bạn không có quyền cập nhật.");
            }

            session.Status = status;
            session.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = status switch
            {
                ClassSessionStatus.Completed => $"Đã xác nhận hoàn thành Buổi {session.SessionNumber}.",
                ClassSessionStatus.Cancelled => $"Đã chuyển Buổi {session.SessionNumber} sang trạng thái Báo nghỉ / Hủy.",
                ClassSessionStatus.Scheduled => $"Đã khôi phục Buổi {session.SessionNumber} về trạng thái Đã lên lịch.",
                _ => $"Đã cập nhật trạng thái Buổi {session.SessionNumber}."
            };

            return RedirectToAction(nameof(Sessions), new { classId, filter });
        }

        private async Task AutoGenerateSessionsIfNeededAsync(Class classItem)
        {
            bool hasSessions = await _context.ClassSessions.AnyAsync(s => s.ClassId == classItem.Id);
            if (hasSessions)
            {
                return;
            }

            var schedules = await _context.ClassSchedules
                .AsNoTracking()
                .Where(cs => cs.ClassId == classItem.Id)
                .Include(cs => cs.TimeSlot)
                .ToListAsync();

            if (!schedules.Any() || classItem.StartDate > classItem.EndDate)
            {
                return;
            }

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
                _context.ClassSessions.AddRange(newSessions);
                await _context.SaveChangesAsync();
            }
        }

        private async Task<TeacherProfile?> GetCurrentTeacherProfileAsync()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return null;
            }

            return await _context.TeacherProfiles
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.UserId == userId);
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
