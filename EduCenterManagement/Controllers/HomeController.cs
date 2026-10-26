
using System.Diagnostics;
using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.ViewModels.Home;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Chỉ lấy lớp đang mở đăng ký.
            var classes = await _context.Classes
                .AsNoTracking()
                .Where(c => c.Status == ClassStatus.Open)
                .Include(c => c.Subject)
                .Include(c => c.Teacher)
                .OrderBy(c => c.StartDate)
                .ToListAsync();

            var classIds = classes.Select(c => c.Id).ToList();

            // Lấy lịch học và khung giờ của những lớp đang mở.
            var schedules = await _context.ClassSchedules
                .AsNoTracking()
                .Where(s => classIds.Contains(s.ClassId))
                .Include(s => s.TimeSlot)
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.TimeSlot.StartTime)
                .ToListAsync();

            var model = classes.Select(c => new OpenClassViewModel
            {
                Id = c.Id,

                // Tạm dùng Name vì Model Class hiện tại
                // chưa có thuộc tính ClassCode.
                ClassCode = c.Name,
                ClassName = c.Name,

                SubjectName = c.Subject?.Name ?? "Chưa xác định",
                TeacherName = c.Teacher?.FullName,

                StartDate = c.StartDate,
                EndDate = c.EndDate,
                MaxStudents = c.MaxStudents,

                Schedules = schedules
                    .Where(s => s.ClassId == c.Id)
                    .Select(s => new OpenClassScheduleViewModel
                    {
                        DayOfWeek = s.DayOfWeek,
                        TimeSlotName = s.TimeSlot.Name,
                        StartTime = s.TimeSlot.StartTime,
                        EndTime = s.TimeSlot.EndTime
                    })
                    .ToList()
            }).ToList();

            return View(model);
        }

        [HttpGet]
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id
                    ?? HttpContext.TraceIdentifier
            });
        }
    }
}
