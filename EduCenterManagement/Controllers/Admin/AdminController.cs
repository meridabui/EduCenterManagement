
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

        // GET: /Admin
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = new AdminDashboardViewModel
            {
                // Teacher
                TotalTeachers = await _context.TeacherProfiles
                    .CountAsync(),

                ActiveTeachers = await _context.TeacherProfiles
                    .CountAsync(t =>
                        t.User.Status == UserStatus.Active &&
                        t.User.Role == UserRole.Teacher),

                // Subject
                TotalSubjects = await _context.Subjects
                    .CountAsync(),

                ActiveSubjects = await _context.Subjects
                    .CountAsync(s => s.Status == SubjectStatus.Active),

                // Class
                TotalClasses = await _context.Classes
                    .CountAsync(c =>
                        c.Status != ClassStatus.Cancelled),

                OpenClasses = await _context.Classes
                    .CountAsync(c => c.Status == ClassStatus.Open),

                ClassesWithoutTeacher = await _context.Classes
                    .CountAsync(c =>
                        c.TeacherId == null &&
                        c.Status != ClassStatus.Cancelled &&
                        c.Status != ClassStatus.Completed),

                CancelledClasses = await _context.Classes
                    .CountAsync(c => c.Status == ClassStatus.Cancelled),

                // TimeSlot
                TotalTimeSlots = await _context.TimeSlots
                    .CountAsync(),

                ActiveTimeSlots = await _context.TimeSlots
                    .CountAsync(t =>
                        t.Status == TimeSlotStatus.Active),

                // ClassSchedule
                TotalClassSchedules = await _context.ClassSchedules
                    .CountAsync()
            };

            return View("~/Views/Admin/Index.cshtml", model);
        }
    }
}
