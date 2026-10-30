using System.Security.Claims;
using EduCenterManagement.Services.Teacher;
using EduCenterManagement.ViewModels.Teacher.Attendance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduCenterManagement.Controllers.Teacher
{
    [Authorize(Roles = "Teacher")]
    [Route("Teacher/TeacherAttendance")]
    public class TeacherAttendanceController : Controller
    {
        private readonly ITeacherAttendanceService _attendanceService;

        public TeacherAttendanceController(ITeacherAttendanceService attendanceService)
        {
            _attendanceService = attendanceService;
        }

        // GET: /Teacher/TeacherAttendance hoặc /Teacher/TeacherAttendance/ClassSessionPickerView
        [HttpGet("")]
        [HttpGet("ClassSessionPickerView")]
        public async Task<IActionResult> ClassSessionPickerView(int? classId = null)
        {
            int userId = GetCurrentUserId();
            var model = await _attendanceService.GetPickerViewModelAsync(userId, classId);
            if (model == null)
            {
                return Forbid();
            }

            return View("~/Views/Teacher/TeacherAttendance/ClassSessionPickerView.cshtml", model);
        }

        // GET: /Teacher/TeacherAttendance/Sheet/{sessionId}
        [HttpGet("Sheet/{sessionId:int}")]
        public async Task<IActionResult> Sheet(int sessionId)
        {
            int userId = GetCurrentUserId();
            var model = await _attendanceService.GetAttendanceSheetAsync(userId, sessionId);
            if (model == null)
            {
                return NotFound("Không tìm thấy buổi học hoặc bạn không có quyền điểm danh lớp này.");
            }

            return View("~/Views/Teacher/TeacherAttendance/Sheet.cshtml", model);
        }

        // POST: /Teacher/TeacherAttendance/SaveSheet
        [HttpPost("SaveSheet")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSheet(AttendanceSheetViewModel model)
        {
            int userId = GetCurrentUserId();
            var (success, message) = await _attendanceService.SaveAttendanceSheetAsync(userId, model);

            if (!success)
            {
                TempData["ErrorMessage"] = message;
                return RedirectToAction(nameof(Sheet), new { sessionId = model.SessionId });
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Sheet), new { sessionId = model.SessionId });
        }

        // AJAX API: Lấy danh sách buổi học theo lớp
        [HttpGet("GetSessionsByClass")]
        public async Task<IActionResult> GetSessionsByClass(int classId)
        {
            int userId = GetCurrentUserId();
            var sessions = await _attendanceService.GetSessionsByClassAsync(userId, classId);
            return Json(sessions);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdClaim, out int userId);
            return userId;
        }
    }
}

