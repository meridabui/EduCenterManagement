using System.Security.Claims;
using EduCenterManagement.Services.Teacher;
using EduCenterManagement.ViewModels.Teacher.Comment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduCenterManagement.Controllers.Teacher
{
    [Authorize(Roles = "Teacher")]
    [Route("Teacher/TeacherComment")]
    public class TeacherCommentController : Controller
    {
        private readonly ITeacherCommentService _commentService;

        public TeacherCommentController(ITeacherCommentService commentService)
        {
            _commentService = commentService;
        }

        // GET: /Teacher/TeacherComment hoặc /Teacher/TeacherComment/ListComment
        [HttpGet("")]
        [HttpGet("ListComment")]
        public async Task<IActionResult> ListComment(int? classId = null)
        {
            int userId = GetCurrentUserId();
            var model = await _commentService.GetClassStudentCommentListAsync(userId, classId);
            if (model == null)
            {
                return Forbid();
            }

            return View("~/Views/Teacher/TeacherComment/ListComment.cshtml", model);
        }

        // GET: /Teacher/TeacherComment/StudentComments?classId=1&studentId=2
        [HttpGet("StudentComments")]
        public async Task<IActionResult> StudentComments(int classId, int studentId)
        {
            int userId = GetCurrentUserId();
            var model = await _commentService.GetStudentCommentDetailAsync(userId, classId, studentId);
            if (model == null)
            {
                return NotFound("Không tìm thấy thông tin học sinh hoặc bạn không có quyền nhận xét cho lớp này.");
            }

            return View("~/Views/Teacher/TeacherComment/StudentComments.cshtml", model);
        }

        // POST: /Teacher/TeacherComment/CreateComment
        [HttpPost("CreateComment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateComment(CreateCommentInputModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(errors) ? "Dữ liệu không hợp lệ." : errors;
                return RedirectToAction(nameof(StudentComments), new { classId = model.ClassId, studentId = model.StudentId });
            }

            int userId = GetCurrentUserId();
            var result = await _commentService.CreateCommentAsync(userId, model);
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(StudentComments), new { classId = model.ClassId, studentId = model.StudentId });
        }

        // POST: /Teacher/TeacherComment/EditComment
        [HttpPost("EditComment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditComment(EditCommentInputModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(errors) ? "Dữ liệu không hợp lệ." : errors;
                return RedirectToAction(nameof(StudentComments), new { classId = model.ClassId, studentId = model.StudentId });
            }

            int userId = GetCurrentUserId();
            var result = await _commentService.UpdateCommentAsync(userId, model);
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(StudentComments), new { classId = model.ClassId, studentId = model.StudentId });
        }

        // POST: /Teacher/TeacherComment/DeleteComment
        [HttpPost("DeleteComment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int id, int classId, int studentId)
        {
            int userId = GetCurrentUserId();
            var result = await _commentService.DeleteCommentAsync(userId, id);
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(StudentComments), new { classId, studentId });
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdClaim, out int userId);
            return userId;
        }
    }
}

