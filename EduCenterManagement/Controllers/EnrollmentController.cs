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
        public async Task<IActionResult> Index(int? studentId, int? subjectId)
        {
            var parent = await GetCurrentParentAsync();

            if (parent == null)
            {
                return Forbid();
            }

            var students = await GetLinkedStudentsAsync(parent.Id);

            // Chỉ chấp nhận học sinh thuộc danh sách đã liên kết.
            if (studentId.HasValue && !students.Any(s => s.Id == studentId.Value))
            {
                studentId = null;
            }

            if (!studentId.HasValue && students.Count == 1)
            {
                studentId = students[0].Id;
            }

            var today = DateOnly.FromDateTime(DateTime.Now);

            var classes = await _context.Classes
                .AsNoTracking()
                .Where(c => c.Status == ClassStatus.Open && c.StartDate > today)
                .Include(c => c.Subject)
                .Include(c => c.Teacher)
                .OrderBy(c => c.StartDate)
                .ToListAsync();
            var classOptions = await BuildClassOptionsAsync(classes, studentId);
            var validSubjectIds = classes.Select(c => c.SubjectId).ToHashSet();
            if (subjectId.HasValue && !validSubjectIds.Contains(subjectId.Value))
                subjectId = null;

            var model = new EnrollmentIndexViewModel
            {
                Students = students,
                SelectedStudentId = studentId,
                SelectedSubjectId = subjectId,
                Subjects = classes
                    .GroupBy(c => new { c.SubjectId, c.Subject.Name, c.Subject.Description })
                    .Select(group =>
                    {
                        var ids = group.Select(c => c.Id).ToHashSet();
                        var options = classOptions.Where(c => ids.Contains(c.ClassId)).ToList();
                        return new SubjectOptionViewModel
                        {
                            SubjectId = group.Key.SubjectId,
                            SubjectName = group.Key.Name,
                            Description = group.Key.Description,
                            OpenClassCount = options.Count,
                            AvailableClassCount = options.Count(c => c.SeatsLeft > 0),
                            TotalSeatsLeft = options.Sum(c => c.SeatsLeft),
                            EarliestStartDate = group.Min(c => c.StartDate),
                            MinimumFee = group.Min(c => c.Fee)
                        };
                    })
                    .OrderBy(subject => subject.SubjectName)
                    .ToList(),
                Classes = subjectId.HasValue
                    ? classOptions.Where(c => classes.Any(entity =>
                        entity.Id == c.ClassId && entity.SubjectId == subjectId.Value)).ToList()
                    : new List<ClassOptionViewModel>()
            };

            return View(model);
        }

        // GET: /Enrollment/Details/5?studentId=1
        [HttpGet]
        public async Task<IActionResult> Details(int id, int? studentId)
        {
            var parent = await GetCurrentParentAsync();
            if (parent == null)
                return Forbid();

            var students = await GetLinkedStudentsAsync(parent.Id);
            if (studentId.HasValue && students.All(student => student.Id != studentId.Value))
                studentId = null;
            if (!studentId.HasValue && students.Count == 1)
                studentId = students[0].Id;

            var today = DateOnly.FromDateTime(DateTime.Now);
            var classItem = await _context.Classes
                .AsNoTracking()
                .Where(c => c.Id == id && c.Status == ClassStatus.Open && c.StartDate > today)
                .Include(c => c.Subject)
                .Include(c => c.Teacher)
                .SingleOrDefaultAsync();

            if (classItem == null)
                return NotFound();

            var option = (await BuildClassOptionsAsync(new List<Class> { classItem }, studentId)).Single();
            return View(new EnrollmentClassDetailsViewModel
            {
                Students = students,
                SelectedStudentId = studentId,
                Class = option
            });
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
                return RedirectToAction(nameof(Details), new { id = classId, studentId });
            }

            if (!result.Success)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction(nameof(Details), new { id = classId, studentId });
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
                StudentId = enrollment.StudentId,
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

        private Task<List<StudentOptionViewModel>> GetLinkedStudentsAsync(int parentId)
        {
            return _context.ParentStudentLinks
                .AsNoTracking()
                .Where(link => link.ParentId == parentId &&
                               link.Status == ParentStudentLinkStatus.Accepted)
                .OrderBy(link => link.Student.FullName)
                .Select(link => new StudentOptionViewModel
                {
                    Id = link.StudentId,
                    FullName = link.Student.FullName
                })
                .ToListAsync();
        }

        private async Task<List<ClassOptionViewModel>> BuildClassOptionsAsync(
            IReadOnlyCollection<Class> classes,
            int? studentId)
        {
            if (classes.Count == 0)
                return new List<ClassOptionViewModel>();

            var now = DateTime.UtcNow;
            var classIds = classes.Select(c => c.Id).ToList();
            var schedules = await _context.ClassSchedules
                .AsNoTracking()
                .Where(schedule => classIds.Contains(schedule.ClassId))
                .Include(schedule => schedule.TimeSlot)
                .OrderBy(schedule => schedule.DayOfWeek)
                .ThenBy(schedule => schedule.TimeSlot.StartTime)
                .ToListAsync();

            var heldByClass = await _context.Enrollments
                .AsNoTracking()
                .Where(enrollment => classIds.Contains(enrollment.ClassId) &&
                    (enrollment.Status == EnrollmentStatus.Confirm ||
                     (enrollment.Status == EnrollmentStatus.PendingPayment && enrollment.ReservedUntil > now)))
                .GroupBy(enrollment => enrollment.ClassId)
                .Select(group => new { ClassId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.ClassId, item => item.Count);

            var registeredClassIds = new HashSet<int>();
            if (studentId.HasValue)
            {
                registeredClassIds = (await _context.Enrollments
                    .AsNoTracking()
                    .Where(enrollment => enrollment.StudentId == studentId.Value &&
                        classIds.Contains(enrollment.ClassId) &&
                        (enrollment.Status == EnrollmentStatus.Confirm ||
                         (enrollment.Status == EnrollmentStatus.PendingPayment && enrollment.ReservedUntil > now)))
                    .Select(enrollment => enrollment.ClassId)
                    .ToListAsync()).ToHashSet();
            }

            return classes.Select(classItem =>
            {
                heldByClass.TryGetValue(classItem.Id, out var held);
                return new ClassOptionViewModel
                {
                    ClassId = classItem.Id,
                    SubjectId = classItem.SubjectId,
                    ClassName = classItem.Name,
                    SubjectName = classItem.Subject.Name,
                    TeacherName = classItem.Teacher?.FullName,
                    StartDate = classItem.StartDate,
                    EndDate = classItem.EndDate,
                    Fee = classItem.Fee,
                    MaxStudents = classItem.MaxStudents,
                    SeatsLeft = Math.Max(0, classItem.MaxStudents - held),
                    AlreadyRegistered = registeredClassIds.Contains(classItem.Id),
                    Schedules = schedules
                        .Where(schedule => schedule.ClassId == classItem.Id)
                        .Select(schedule => new ClassScheduleItemViewModel
                        {
                            DayOfWeek = schedule.DayOfWeek,
                            TimeSlotName = schedule.TimeSlot.Name,
                            StartTime = schedule.TimeSlot.StartTime,
                            EndTime = schedule.TimeSlot.EndTime
                        }).ToList()
                };
            }).ToList();
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
