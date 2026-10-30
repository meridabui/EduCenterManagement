using System.Security.Claims;
using EduCenterManagement.Services.Teacher;
using EduCenterManagement.ViewModels.Teacher.Slide;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduCenterManagement.Controllers.Teacher
{
    [Authorize(Roles = "Teacher")]
    [Route("Teacher/TeacherSlide")]
    public class TeacherSlideController : Controller
    {
        private readonly ITeacherSlideService _slideService;

        public TeacherSlideController(ITeacherSlideService slideService)
        {
            _slideService = slideService;
        }

        // GET: /Teacher/TeacherSlide hoặc /Teacher/TeacherSlide/ClassSessionPickerView
        [HttpGet("")]
        [HttpGet("ClassSessionPickerView")]
        public async Task<IActionResult> ClassSessionPickerView(int? classId = null, int? sessionId = null)
        {
            int userId = GetCurrentUserId();
            var model = await _slideService.GetClassSessionPickerDataAsync(userId, classId, sessionId);
            return View("~/Views/Teacher/TeacherSlide/ClassSessionPickerView.cshtml", model);
        }

        // GET: /Teacher/TeacherSlide/GetSessionsByClass?classId=123 (AJAX)
        [HttpGet("GetSessionsByClass")]
        public async Task<IActionResult> GetSessionsByClass(int classId)
        {
            int userId = GetCurrentUserId();
            var sessions = await _slideService.GetSessionsDropdownByClassIdAsync(userId, classId);
            return Json(sessions);
        }

        // GET: /Teacher/TeacherSlide/SessionSlides/{sessionId}
        [HttpGet("SessionSlides/{sessionId:int}")]
        public async Task<IActionResult> SessionSlides(int sessionId)
        {
            int userId = GetCurrentUserId();
            var model = await _slideService.GetSessionSlidesAsync(userId, sessionId);
            if (model == null)
            {
                return NotFound("Không tìm thấy buổi học hoặc bạn không có quyền truy cập tài liệu buổi học này.");
            }

            return View("~/Views/Teacher/TeacherSlide/SessionSlides.cshtml", model);
        }

        // POST: /Teacher/TeacherSlide/Upload
        [HttpPost("Upload")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(UploadSlideInputModel model, [FromForm(Name = "UploadInput")] UploadSlideInputModel? uploadInput = null)
        {
            var input = (model != null && model.SessionId > 0) ? model : (uploadInput ?? model ?? new UploadSlideInputModel());

            if (input.SessionId <= 0)
            {
                TempData["ErrorMessage"] = "Không xác định được buổi học cần tải tài liệu lên.";
                return RedirectToAction(nameof(ClassSessionPickerView));
            }

            if (string.IsNullOrWhiteSpace(input.Title) || input.File == null || input.File.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng nhập tiêu đề và chọn tệp tin cần tải lên.";
                return RedirectToAction(nameof(SessionSlides), new { sessionId = input.SessionId });
            }

            int userId = GetCurrentUserId();
            var result = await _slideService.UploadSlideAsync(userId, input);
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(SessionSlides), new { sessionId = input.SessionId });
        }

        // POST: /Teacher/TeacherSlide/Delete/{id}
        [HttpPost("Delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int sessionId)
        {
            int userId = GetCurrentUserId();
            var result = await _slideService.DeleteSlideAsync(userId, id);
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(SessionSlides), new { sessionId });
        }

        // GET: /Teacher/TeacherSlide/Download/{id}
        [HttpGet("Download/{id:int}")]
        public async Task<IActionResult> Download(int id)
        {
            int userId = GetCurrentUserId();
            var fileInfo = await _slideService.GetSlideFileForDownloadAsync(userId, id);
            if (!fileInfo.Success || string.IsNullOrEmpty(fileInfo.PhysicalPath))
            {
                TempData["ErrorMessage"] = "Tệp tin không tồn tại hoặc bạn không có quyền tải tệp này.";
                return RedirectToAction(nameof(ClassSessionPickerView));
            }

            return PhysicalFile(fileInfo.PhysicalPath, fileInfo.MimeType ?? "application/octet-stream", fileInfo.OriginalFileName);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdClaim, out int userId);
            return userId;
        }
    }
}

