using System.Security.Claims;
using EduCenterManagement.Services.Student;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduCenterManagement.Controllers.Student
{
    [Authorize(Roles = "Student")]
    public class StudentQuizController : Controller
    {
        private readonly IStudentQuizService _quizService;

        public StudentQuizController(IStudentQuizService quizService)
        {
            _quizService = quizService;
        }

        // GET: /StudentQuiz
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!TryGetUserId(out int userId)) return Forbid();

            var model = await _quizService.GetQuizListAsync(userId);
            if (model == null) return Forbid();

            return View("~/Views/Student/StudentQuiz/Index.cshtml", model);
        }

        // POST: /StudentQuiz/Start/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int id)
        {
            if (!TryGetUserId(out int userId)) return Forbid();

            var (success, message, attemptId) = await _quizService.StartAttemptAsync(userId, id);
            if (!success || attemptId == null)
            {
                TempData["Error"] = message;
                return RedirectToAction(nameof(Index));
            }

            return RedirectToAction(nameof(Take), new { id = attemptId });
        }

        // GET: /StudentQuiz/Take/{attemptId}
        [HttpGet]
        public async Task<IActionResult> Take(int id)
        {
            if (!TryGetUserId(out int userId)) return Forbid();

            var model = await _quizService.GetTakeQuizAsync(userId, id);
            if (model == null)
            {
                // Đã nộp hoặc đã hết giờ -> xem kết quả (nếu có quyền)
                var result = await _quizService.GetResultAsync(userId, id);
                if (result != null) return RedirectToAction(nameof(Result), new { id });

                TempData["Error"] = "Không tìm thấy lượt làm bài.";
                return RedirectToAction(nameof(Index));
            }

            return View("~/Views/Student/StudentQuiz/Take.cshtml", model);
        }

        // POST: /StudentQuiz/Submit/{attemptId}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int id, Dictionary<int, int>? answers, bool isAutoSubmit = false)
        {
            if (!TryGetUserId(out int userId)) return Forbid();

            var (success, message) = await _quizService.SubmitAttemptAsync(
                userId, id, answers ?? new Dictionary<int, int>(), isAutoSubmit);

            TempData[success ? "Success" : "Error"] = message;

            var result = await _quizService.GetResultAsync(userId, id);
            if (result != null) return RedirectToAction(nameof(Result), new { id });

            return RedirectToAction(nameof(Index));
        }

        // GET: /StudentQuiz/Result/{attemptId}
        [HttpGet]
        public async Task<IActionResult> Result(int id)
        {
            if (!TryGetUserId(out int userId)) return Forbid();

            var model = await _quizService.GetResultAsync(userId, id);
            if (model == null)
            {
                TempData["Error"] = "Không tìm thấy kết quả bài làm.";
                return RedirectToAction(nameof(Index));
            }

            return View("~/Views/Student/StudentQuiz/Result.cshtml", model);
        }

        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdText, out userId);
        }
    }
}
