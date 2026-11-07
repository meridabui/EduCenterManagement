using System.Security.Claims;
using EduCenterManagement.Enums;
using EduCenterManagement.Services.Teacher;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduCenterManagement.Controllers.Teacher
{
    [Authorize(Roles = "Teacher")]
    [Route("Teacher")]
    public class TeacherController : Controller
    {
        private readonly ITeacherService _teacherService;

        public TeacherController(ITeacherService teacherService)
        {
            _teacherService = teacherService;
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
            int userId = GetCurrentUserId();
            var model = await _teacherService.GetAssignedClassesAsync(userId);
            if (model == null)
            {
                return Forbid();
            }

            return View("~/Views/Teacher/AssignedClasses.cshtml", model);
        }

        // GET: /Teacher/StudentList/{classId}
        [HttpGet("StudentList/{classId:int}")]
        public async Task<IActionResult> StudentList(int classId)
        {
            int userId = GetCurrentUserId();
            var model = await _teacherService.GetStudentListAsync(userId, classId);
            if (model == null)
            {
                return NotFound("Lớp học không tồn tại hoặc bạn không được phân công phụ trách lớp này.");
            }

            return View("~/Views/Teacher/StudentList.cshtml", model);
        }

        // GET: /Teacher/Timetable
        [HttpGet("Timetable")]
        public async Task<IActionResult> Timetable(int weekOffset = 0)
        {
            int userId = GetCurrentUserId();
            var model = await _teacherService.GetTimetableAsync(userId, weekOffset);
            if (model == null)
            {
                return Forbid();
            }

            return View("~/Views/Teacher/Timetable.cshtml", model);
        }

        // GET: /Teacher/Sessions/{classId}
        [HttpGet("Sessions/{classId:int}")]
        public async Task<IActionResult> Sessions(int classId, string filter = "all")
        {
            int userId = GetCurrentUserId();
            var model = await _teacherService.GetSessionsAsync(userId, classId, filter);
            if (model == null)
            {
                return NotFound("Lớp học không tồn tại hoặc bạn không được phân công phụ trách lớp này.");
            }

            return View("~/Views/Teacher/Sessions.cshtml", model);
        }

        // POST: /Teacher/UpdateSessionStatus
        [HttpPost("UpdateSessionStatus")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSessionStatus(int sessionId, ClassSessionStatus status, int classId, string filter = "all")
        {
            int userId = GetCurrentUserId();
            var (success, message, _) = await _teacherService.UpdateSessionStatusAsync(userId, sessionId, classId, status);

            if (!success)
            {
                TempData["ErrorMessage"] = message;
            }
            else
            {
                TempData["SuccessMessage"] = message;
            }

            return RedirectToAction(nameof(Sessions), new { classId, filter });
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdClaim, out int userId);
            return userId;
        }
    }
}
