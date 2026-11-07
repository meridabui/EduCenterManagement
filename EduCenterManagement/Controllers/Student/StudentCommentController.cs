using System.Security.Claims;
using EduCenterManagement.Services.Student;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduCenterManagement.Controllers.Student
{
    [Authorize(Roles = "Student")]
    public class StudentCommentController : Controller
    {
        private readonly IStudentCommentService _commentService;

        public StudentCommentController(IStudentCommentService commentService)
        {
            _commentService = commentService;
        }

        // GET: /StudentComment?classId=1
        [HttpGet]
        public async Task<IActionResult> Index(int? classId)
        {
            var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdText, out int userId)) return Forbid();

            var model = await _commentService.GetCommentsAsync(userId, classId);
            if (model == null) return Forbid();

            return View("~/Views/StudentComment/Index.cshtml", model);
        }
    }
}
