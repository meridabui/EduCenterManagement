using System.Security.Claims;
using EduCenterManagement.Enums;
using EduCenterManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduCenterManagement.Controllers
{
    // BR13: chỉ Parent mới đăng ký lớp, và chỉ cho Student đã liên kết.
    [Authorize(Roles = "Parent")]
    public class EnrollmentController : Controller
    {
        private readonly IEnrollmentService _enrollmentService;
        private readonly ILogger<EnrollmentController> _logger;

        public EnrollmentController(
            IEnrollmentService enrollmentService,
            ILogger<EnrollmentController> logger)
        {
            _enrollmentService = enrollmentService;
            _logger = logger;
        }

        // GET: /Enrollment?studentId=1
        [HttpGet]
        public async Task<IActionResult> Index(int? studentId, int? subjectId)
        {
            if (!TryGetUserId(out var userId))
                return Forbid();

            var result = await _enrollmentService.GetIndexAsync(
                userId,
                studentId,
                subjectId);

            if (result.Status == EnrollmentReadStatus.Forbidden)
                return Forbid();

            return View(result.Data);
        }

        // GET: /Enrollment/Details/5?studentId=1
        [HttpGet]
        public async Task<IActionResult> Details(int id, int? studentId)
        {
            if (!TryGetUserId(out var userId))
                return Forbid();

            var result = await _enrollmentService.GetClassDetailsAsync(
                userId,
                id,
                studentId);

            return result.Status switch
            {
                EnrollmentReadStatus.Forbidden => Forbid(),
                EnrollmentReadStatus.NoStudentSelected => RedirectToAction(nameof(Index)),
                EnrollmentReadStatus.NotFound => NotFound(),
                _ => View(result.Data)
            };
        }

        // POST: /Enrollment/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(int studentId, int classId)
        {
            if (!TryGetUserId(out var userId))
                return Forbid();

            var parentId = await _enrollmentService.GetParentIdAsync(userId);

            if (parentId == null)
            {
                return Forbid();
            }

            EnrollmentResult result;

            try
            {
                result = await _enrollmentService.RegisterAsync(parentId.Value, studentId, classId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Register enrollment crashed.");
                TempData["Error"] = "Có lỗi hệ thống khi đăng ký. Vui lòng thử lại.";
                return RedirectToAction(nameof(Details), new { id = classId, studentId });
            }

            if (!result.Success)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction(nameof(Details), new { id = classId, studentId });
            }

            return RedirectToAction(nameof(Confirmation), new { id = result.Enrollment!.Id });
        }

        // GET: /Enrollment/History
        [HttpGet]
        public async Task<IActionResult> History(
            int? studentId,
            EnrollmentStatus? status)
        {
            if (!TryGetUserId(out var userId))
                return Forbid();

            var result = await _enrollmentService.GetHistoryAsync(
                userId,
                studentId,
                status);

            if (result.Status == EnrollmentReadStatus.Forbidden)
                return Forbid();

            return View(result.Data);
        }

        // GET: /Enrollment/Confirmation/5
        [HttpGet]
        public async Task<IActionResult> Confirmation(int id)
        {
            if (!TryGetUserId(out var userId))
                return Forbid();

            var result = await _enrollmentService.GetConfirmationAsync(userId, id);

            return result.Status switch
            {
                EnrollmentReadStatus.Forbidden => Forbid(),
                EnrollmentReadStatus.NotFound => NotFound(),
                _ => View(result.Data)
            };
        }

        private bool TryGetUserId(out int userId)
        {
            return int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out userId);
        }
    }
}
