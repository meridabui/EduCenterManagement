
using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var now = DateTime.UtcNow;

            var classes = await _context.Classes
                .AsNoTracking()
                .Where(c => c.Status != ClassStatus.Cancelled)
                .Include(c => c.Subject)
                .Include(c => c.Teacher)
                .Include(c => c.ClassSchedules)
                    .ThenInclude(s => s.TimeSlot)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var classIds = classes.Select(c => c.Id).ToList();

            var enrollmentCounts = await _context.Enrollments
                .AsNoTracking()
                .Where(e =>
                    classIds.Contains(e.ClassId) &&
                    (
                        e.Status == EnrollmentStatus.Confirm ||
                        (
                            e.Status == EnrollmentStatus.PendingPayment &&
                            (e.ReservedUntil == null ||
                             e.ReservedUntil > now)
                        )
                    ))
                .GroupBy(e => new { e.ClassId, e.Status })
                .Select(g => new
                {
                    g.Key.ClassId,
                    g.Key.Status,
                    Count = g.Count()
                })
                .ToListAsync();

            int CountOf(int classId, EnrollmentStatus status) =>
                enrollmentCounts
                    .Where(x => x.ClassId == classId &&
                                x.Status == status)
                    .Select(x => x.Count)
                    .FirstOrDefault();

            var rows = classes.Select(c =>
            {
                var schedules = c.ClassSchedules
                    .OrderBy(s => s.DayOfWeek)
                    .ToList();

                var firstSchedule = schedules.FirstOrDefault();

                return new DashboardClassRow
                {
                    ClassId = c.Id,
                    Name = c.Name,
                    SubjectName = c.Subject?.Name ?? "Chưa có môn học",
                    Fee = c.Fee,

                    DayLabels = schedules
                        .Select(s => s.DayOfWeek == 8
                            ? "CN"
                            : $"Thứ {s.DayOfWeek}")
                        .Distinct()
                        .ToList(),

                    SlotText = firstSchedule?.TimeSlot is { } slot
                        ? $"{slot.Name} ({slot.StartTime:HH\\:mm} - " +
                          $"{slot.EndTime:HH\\:mm})"
                        : "Chưa có lịch",

                    TeacherName = c.Teacher?.FullName,

                    Confirmed = CountOf(c.Id, EnrollmentStatus.Confirm),
                    Reserved = CountOf(c.Id, EnrollmentStatus.PendingPayment),
                    MaxStudents = c.MaxStudents
                };
            }).ToList();

            var model = new AdminDashboardViewModel
            {
                TotalTeachers = await _context.TeacherProfiles.CountAsync(),

                ActiveTeachers = await _context.TeacherProfiles.CountAsync(t =>
                    t.User.Status == UserStatus.Active &&
                    t.User.Role == UserRole.Teacher),

                TotalSubjects = await _context.Subjects.CountAsync(),

                ActiveSubjects = await _context.Subjects.CountAsync(s =>
                    s.Status == SubjectStatus.Active),

                TotalClasses = rows.Count,

                ClassesWithTeacher = rows.Count(r =>
                    !string.IsNullOrWhiteSpace(r.TeacherName)),

                ClassesWithoutTeacher = rows.Count(r =>
                    string.IsNullOrWhiteSpace(r.TeacherName)),

                OpenClasses = classes.Count(c =>
                    c.Status == ClassStatus.Open),

                CancelledClasses = await _context.Classes.CountAsync(c =>
                    c.Status == ClassStatus.Cancelled),

                TotalTimeSlots = await _context.TimeSlots.CountAsync(),

                ActiveTimeSlots = await _context.TimeSlots.CountAsync(t =>
                    t.Status == TimeSlotStatus.Active),

                TotalClassSchedules =
                    await _context.ClassSchedules.CountAsync(),

                TotalSeats = rows.Sum(r => r.MaxStudents),
                OccupiedSeats = rows.Sum(r => r.Occupied),

                Classes = rows
            };

            return View("~/Views/Admin/Index.cshtml", model);
        }
    }
}
