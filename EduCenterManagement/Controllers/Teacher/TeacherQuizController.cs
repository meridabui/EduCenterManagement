using System.Security.Claims;
using EduCenterManagement.Enums;
using EduCenterManagement.Services.Teacher;
using EduCenterManagement.ViewModels.Teacher.Quiz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduCenterManagement.Controllers.Teacher
{
    [Authorize(Roles = "Teacher")]
    [Route("Teacher/TeacherQuiz")]
    public class TeacherQuizController : Controller
    {
        private readonly ITeacherQuizService _quizService;

        public TeacherQuizController(ITeacherQuizService quizService)
        {
            _quizService = quizService;
        }

        // GET: /Teacher/TeacherQuiz hoặc /Teacher/TeacherQuiz/QuizList
        [HttpGet("")]
        [HttpGet("QuizList")]
        public async Task<IActionResult> QuizList(string filter = "my", int? subjectId = null, string? search = null)
        {
            int userId = GetCurrentUserId();
            var model = await _quizService.GetQuizListAsync(userId, filter, subjectId, search);
            if (model == null)
            {
                return Forbid();
            }

            return View("~/Views/Teacher/TeacherQuiz/QuizList.cshtml", model);
        }

        // GET: /Teacher/TeacherQuiz/Create
        [HttpGet("Create")]
        public async Task<IActionResult> Create(int? sessionId = null, int? classId = null)
        {
            int userId = GetCurrentUserId();
            var model = await _quizService.GetCreateQuizViewModelAsync(userId, sessionId, classId);
            if (model == null)
            {
                return Forbid();
            }

            return View("~/Views/Teacher/TeacherQuiz/Create.cshtml", model);
        }

        // POST: /Teacher/TeacherQuiz/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateQuizViewModel model)
        {
            int userId = GetCurrentUserId();

            if (!ModelState.IsValid)
            {
                var refreshed = await _quizService.GetCreateQuizViewModelAsync(userId, model.SessionId, model.ClassId);
                if (refreshed != null)
                {
                    model.Classes = refreshed.Classes;
                    model.Sessions = refreshed.Sessions;
                }
                return View("~/Views/Teacher/TeacherQuiz/Create.cshtml", model);
            }

            var (success, message, newQuizId) = await _quizService.CreateQuizAsync(userId, model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, message);
                var refreshed = await _quizService.GetCreateQuizViewModelAsync(userId, model.SessionId, model.ClassId);
                if (refreshed != null)
                {
                    model.Classes = refreshed.Classes;
                    model.Sessions = refreshed.Sessions;
                }
                return View("~/Views/Teacher/TeacherQuiz/Create.cshtml", model);
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Details), new { id = newQuizId, tab = "questions" });
        }

        // GET: /Teacher/TeacherQuiz/Details/{id}
        [HttpGet("Details/{id:int}")]
        public async Task<IActionResult> Details(int id, string tab = "questions")
        {
            int userId = GetCurrentUserId();
            var model = await _quizService.GetQuizDetailAsync(userId, id, tab);
            if (model == null)
            {
                return NotFound("Bài Quiz không tồn tại hoặc bạn không có quyền truy cập.");
            }

            return View("~/Views/Teacher/TeacherQuiz/Details.cshtml", model);
        }

        // POST: /Teacher/TeacherQuiz/AddQuestion
        [HttpPost("AddQuestion")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddQuestion(QuizQuestionFormViewModel model)
        {
            int userId = GetCurrentUserId();
            var (success, message) = await _quizService.AddQuestionAsync(userId, model);

            if (!success)
            {
                TempData["ErrorMessage"] = message;
            }
            else
            {
                TempData["SuccessMessage"] = message;
            }

            return RedirectToAction(nameof(Details), new { id = model.QuizId, tab = "questions" });
        }

        // POST: /Teacher/TeacherQuiz/DeleteQuestion
        [HttpPost("DeleteQuestion")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteQuestion(int questionId, int quizId)
        {
            int userId = GetCurrentUserId();
            var (success, message) = await _quizService.DeleteQuestionAsync(userId, questionId, quizId);

            if (!success)
            {
                TempData["ErrorMessage"] = message;
            }
            else
            {
                TempData["SuccessMessage"] = message;
            }

            return RedirectToAction(nameof(Details), new { id = quizId, tab = "questions" });
        }

        // GET: /Teacher/TeacherQuiz/Clone/{id}
        [HttpGet("Clone/{id:int}")]
        public async Task<IActionResult> Clone(int id)
        {
            int userId = GetCurrentUserId();
            var model = await _quizService.GetCloneViewModelAsync(userId, id);
            if (model == null)
            {
                return NotFound("Không tìm thấy bài Quiz nguồn cần nhân bản.");
            }

            return View("~/Views/Teacher/TeacherQuiz/Clone.cshtml", model);
        }

        // POST: /Teacher/TeacherQuiz/Clone
        [HttpPost("Clone")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Clone(CloneQuizViewModel model)
        {
            int userId = GetCurrentUserId();

            if (!ModelState.IsValid)
            {
                var refreshed = await _quizService.GetCloneViewModelAsync(userId, model.SourceQuizId);
                if (refreshed != null)
                {
                    model.TargetClasses = refreshed.TargetClasses;
                    model.TargetSessions = refreshed.TargetSessions;
                }
                return View("~/Views/Teacher/TeacherQuiz/Clone.cshtml", model);
            }

            var (success, message, newQuizId) = await _quizService.CloneQuizAsync(userId, model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, message);
                var refreshed = await _quizService.GetCloneViewModelAsync(userId, model.SourceQuizId);
                if (refreshed != null)
                {
                    model.TargetClasses = refreshed.TargetClasses;
                    model.TargetSessions = refreshed.TargetSessions;
                }
                return View("~/Views/Teacher/TeacherQuiz/Clone.cshtml", model);
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Details), new { id = newQuizId, tab = "questions" });
        }

        // POST: /Teacher/TeacherQuiz/UpdateStatus
        [HttpPost("UpdateStatus")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int quizId, QuizStatus status)
        {
            int userId = GetCurrentUserId();
            var (success, message) = await _quizService.UpdateStatusAsync(userId, quizId, status);

            if (!success)
            {
                TempData["ErrorMessage"] = message;
            }
            else
            {
                TempData["SuccessMessage"] = message;
            }

            return RedirectToAction(nameof(Details), new { id = quizId });
        }

        // POST: /Teacher/TeacherQuiz/Delete/{id}
        [HttpPost("Delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            int userId = GetCurrentUserId();
            var (success, message) = await _quizService.DeleteQuizAsync(userId, id);

            if (!success)
            {
                TempData["ErrorMessage"] = message;
            }
            else
            {
                TempData["SuccessMessage"] = message;
            }

            return RedirectToAction(nameof(QuizList));
        }

        // GET: /Teacher/TeacherQuiz/StudentAttempt/{attemptId}
        [HttpGet("StudentAttempt/{attemptId:int}")]
        public async Task<IActionResult> StudentAttempt(int attemptId)
        {
            int userId = GetCurrentUserId();
            var model = await _quizService.GetStudentAttemptDetailAsync(userId, attemptId);
            if (model == null)
            {
                return NotFound("Không tìm thấy lượt làm bài hoặc bạn không có quyền xem.");
            }

            return View("~/Views/Teacher/TeacherQuiz/StudentAttempt.cshtml", model);
        }

        // AJAX API: Lấy danh sách buổi học theo lớp
        [HttpGet("GetSessionsByClass")]
        public async Task<IActionResult> GetSessionsByClass(int classId)
        {
            int userId = GetCurrentUserId();
            var sessions = await _quizService.GetSessionsByClassAsync(userId, classId);
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
