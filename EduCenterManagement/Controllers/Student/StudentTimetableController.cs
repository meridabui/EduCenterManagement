using System.Security.Claims;
using EduCenterManagement.Services.Student;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduCenterManagement.Controllers.Student
{
    [Authorize(Roles = "Student")]
    public class StudentTimetableController : Controller
    {
        private readonly IStudentTimetableService _timetableService;
        private readonly IWebHostEnvironment _env;

        public StudentTimetableController(
            IStudentTimetableService timetableService,
            IWebHostEnvironment env)
        {
            _timetableService = timetableService;
            _env = env;
        }

        // GET: /StudentTimetable/Index?weekOffset=0
        [HttpGet]
        public async Task<IActionResult> Index(string? weekOffset)
        {
            if (!TryGetUserId(out int userId))
            {
                return Forbid();
            }

            int offset = 0;
            if (!string.IsNullOrEmpty(weekOffset) && int.TryParse(weekOffset, out int parsedOffset))
            {
                offset = parsedOffset;
            }

            var viewModel = await _timetableService.GetWeeklyTimetableAsync(userId, offset);
            if (viewModel == null)
            {
                return Forbid();
            }

            ViewBag.WeekOffset = offset;
            return View("~/Views/StudentDashboard/WeeklyTimetable.cshtml", viewModel);
        }

        // GET: /StudentTimetable/ClassDetail/{id}
        [HttpGet]
        public async Task<IActionResult> ClassDetail(int id)
        {
            if (!TryGetUserId(out int userId))
            {
                return Forbid();
            }

            var viewModel = await _timetableService.GetClassDetailAsync(userId, id);
            if (viewModel == null)
            {
                TempData["Error"] = "Bạn chưa có quyền truy cập lớp học này hoặc lớp không tồn tại (chỉ dành cho lớp đã thanh toán).";
                return RedirectToAction(nameof(Index));
            }

            return View("~/Views/StudentDashboard/ClassDetail.cshtml", viewModel);
        }

        // GET: /StudentTimetable/DownloadSlide/{slideId}
        [HttpGet]
        public async Task<IActionResult> DownloadSlide(int slideId)
        {
            if (!TryGetUserId(out int userId))
            {
                return Forbid();
            }

            var (fileContent, physicalPath, contentType, fileName) =
                await _timetableService.GetSlideDownloadAsync(userId, slideId, _env.WebRootPath);

            if (fileContent != null)
            {
                return File(fileContent, contentType, fileName);
            }

            if (!string.IsNullOrEmpty(physicalPath))
            {
                return PhysicalFile(physicalPath, contentType, fileName);
            }

            TempData["Error"] = "Bạn không có quyền tải tài liệu này hoặc tài liệu không tồn tại.";
            return RedirectToAction(nameof(Index));
        }

        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdText, out userId);
        }
    }
}
