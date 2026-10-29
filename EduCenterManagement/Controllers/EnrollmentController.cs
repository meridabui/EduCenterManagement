using System.Net;
using System.Security.Claims;
using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.Services;
using EduCenterManagement.ViewModels.Registration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Controllers
{
    // BR13: chỉ Parent mới đăng ký lớp, và chỉ cho Student đã liên kết.
    [Authorize(Roles = "Parent")]
    public class EnrollmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEnrollmentService _enrollmentService;
        private readonly IAppEmailSender _emailSender;
        private readonly ILogger<EnrollmentController> _logger;

        public EnrollmentController(
            ApplicationDbContext context,
            IEnrollmentService enrollmentService,
            IAppEmailSender emailSender,
            ILogger<EnrollmentController> logger)
        {
            _context = context;
            _enrollmentService = enrollmentService;
            _emailSender = emailSender;
            _logger = logger;
        }

        // GET: /Enrollment?studentId=1
        [HttpGet]
        public async Task<IActionResult> Index(int? studentId)
        {
            var parent = await GetCurrentParentAsync();

            if (parent == null)
            {
                return Forbid();
            }

            var students = await _context.ParentStudentLinks
                .AsNoTracking()
                .Where(l => l.ParentId == parent.Id &&
                            l.Status == ParentStudentLinkStatus.Accepted)
                .OrderBy(l => l.Student.FullName)
                .Select(l => new StudentOptionViewModel
                {
                    Id = l.StudentId,
                    FullName = l.Student.FullName
                })
                .ToListAsync();

            // Chỉ chấp nhận học sinh thuộc danh sách đã liên kết.
            if (studentId.HasValue && !students.Any(s => s.Id == studentId.Value))
            {
                studentId = null;
            }

            if (!studentId.HasValue && students.Count == 1)
            {
                studentId = students[0].Id;
            }

            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(DateTime.Now);

            var classes = await _context.Classes
                .AsNoTracking()
                .Where(c => c.Status == ClassStatus.Open && c.StartDate > today)
                .Include(c => c.Subject)
                .Include(c => c.Teacher)
                .OrderBy(c => c.StartDate)
                .ToListAsync();

            var classIds = classes.Select(c => c.Id).ToList();

            var schedules = await _context.ClassSchedules
                .AsNoTracking()
                .Where(s => classIds.Contains(s.ClassId))
                .Include(s => s.TimeSlot)
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.TimeSlot.StartTime)
                .ToListAsync();

            // BR17: số chỗ đã giữ = Paid + Pending còn trong thời gian giữ chỗ.
            var heldByClass = await _context.Enrollments
                .AsNoTracking()
                .Where(e => classIds.Contains(e.ClassId) &&
                            (e.Status == EnrollmentStatus.Confirm ||
                             (e.Status == EnrollmentStatus.PendingPayment && e.ReservedUntil > now)))
                .GroupBy(e => e.ClassId)
                .Select(g => new { ClassId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ClassId, x => x.Count);

            var registeredClassIds = new HashSet<int>();

            if (studentId.HasValue)
            {
                var ids = await _context.Enrollments
                    .AsNoTracking()
                    .Where(e => e.StudentId == studentId.Value &&
                                classIds.Contains(e.ClassId) &&
                                (e.Status == EnrollmentStatus.Confirm ||
                                 (e.Status == EnrollmentStatus.PendingPayment && e.ReservedUntil > now)))
                    .Select(e => e.ClassId)
                    .ToListAsync();

                registeredClassIds = ids.ToHashSet();
            }

            var model = new EnrollmentIndexViewModel
            {
                Students = students,
                SelectedStudentId = studentId,
                Classes = classes.Select(c =>
                {
                    heldByClass.TryGetValue(c.Id, out var held);

                    return new ClassOptionViewModel
                    {
                        ClassId = c.Id,
                        ClassName = c.Name,
                        SubjectName = c.Subject?.Name ?? "Chưa xác định",
                        TeacherName = c.Teacher?.FullName,
                        StartDate = c.StartDate,
                        EndDate = c.EndDate,
                        Fee = c.Fee,
                        MaxStudents = c.MaxStudents,
                        SeatsLeft = Math.Max(0, c.MaxStudents - held),
                        AlreadyRegistered = registeredClassIds.Contains(c.Id),
                        Schedules = schedules
                            .Where(s => s.ClassId == c.Id)
                            .Select(s => new ClassScheduleItemViewModel
                            {
                                DayOfWeek = s.DayOfWeek,
                                TimeSlotName = s.TimeSlot.Name,
                                StartTime = s.TimeSlot.StartTime,
                                EndTime = s.TimeSlot.EndTime
                            })
                            .ToList()
                    };
                }).ToList()
            };

            return View(model);
        }

        // POST: /Enrollment/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(int studentId, int classId)
        {
            var parent = await GetCurrentParentAsync();

            if (parent == null)
            {
                return Forbid();
            }

            EnrollmentResult result;

            try
            {
                result = await _enrollmentService.RegisterAsync(parent.Id, studentId, classId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Register enrollment crashed.");
                TempData["Error"] = "Có lỗi hệ thống khi đăng ký. Vui lòng thử lại.";
                return RedirectToAction(nameof(Index), new { studentId });
            }

            if (!result.Success)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction(nameof(Index), new { studentId });
            }

            await TrySendReservationEmailAsync(result);

            return RedirectToAction(nameof(Confirmation), new { id = result.Enrollment!.Id });
        }

        // GET: /Enrollment/Confirmation/5
        [HttpGet]
        public async Task<IActionResult> Confirmation(int id)
        {
            var parent = await GetCurrentParentAsync();

            if (parent == null)
            {
                return Forbid();
            }

            var enrollment = await _context.Enrollments
                .AsNoTracking()
                .Include(e => e.Student)
                .Include(e => e.Class)
                    .ThenInclude(c => c.Subject)
                .SingleOrDefaultAsync(e =>
                    e.Id == id && e.RegisteredByParentId == parent.Id);

            if (enrollment == null)
            {
                return NotFound();
            }

            var reservedUntil = enrollment.ReservedUntil.HasValue
                ? DateTime.SpecifyKind(enrollment.ReservedUntil.Value, DateTimeKind.Utc)
                : (DateTime?)null;

            return View(new EnrollmentConfirmationViewModel
            {
                EnrollmentId = enrollment.Id,
                StudentName = enrollment.Student.FullName,
                ClassName = enrollment.Class.Name,
                SubjectName = enrollment.Class.Subject.Name,
                Fee = enrollment.Class.Fee,
                Status = enrollment.Status,
                ReservedUntil = reservedUntil,
                IsHoldActive = enrollment.Status == EnrollmentStatus.PendingPayment &&
                               reservedUntil.HasValue &&
                               reservedUntil.Value > DateTime.UtcNow
            });
        }

        private async Task<ParentProfile?> GetCurrentParentAsync()
        {
            var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdText, out var userId))
            {
                return null;
            }

            return await _context.ParentProfiles
                .AsNoTracking()
                .SingleOrDefaultAsync(p => p.UserId == userId);
        }

        // Email xác nhận giữ chỗ gửi cho Parent. Lỗi gửi email không làm hỏng đăng ký.
        private async Task TrySendReservationEmailAsync(EnrollmentResult result)
        {
            try
            {
                var toEmail = User.FindFirstValue(ClaimTypes.Email);

                if (string.IsNullOrWhiteSpace(toEmail) || result.Enrollment == null)
                {
                    return;
                }

                var until = result.Enrollment.ReservedUntil!.Value
                    .ToLocalTime()
                    .ToString("HH:mm dd/MM/yyyy");

                var body =
                    "<p>Đăng ký lớp học đã được tạo thành công.</p>" +
                    $"<p>Học sinh: <strong>{WebUtility.HtmlEncode(result.StudentName)}</strong><br/>" +
                    $"Lớp: <strong>{WebUtility.HtmlEncode(result.ClassName)}</strong></p>" +
                    $"<p>Chỗ của học sinh được giữ đến <strong>{until}</strong> " +
                    $"({EnrollmentService.HoldMinutes} phút). Nếu chưa thanh toán trước thời điểm này, " +
                    "chỗ sẽ được nhả và đăng ký tự động bị hủy.</p>";

                await _emailSender.SendAsync(
                    toEmail,
                    "Xác nhận giữ chỗ lớp học - EduCenter",
                    body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send reservation email.");
            }
        }
    }
}
