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
        private readonly ILogger<EnrollmentController> _logger;

        public EnrollmentController(
            ApplicationDbContext context,
            IEnrollmentService enrollmentService,
            ILogger<EnrollmentController> logger)
        {
            _context = context;
            _enrollmentService = enrollmentService;
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

            var classes = new List<Class>();
            var classOptions = new List<ClassOptionViewModel>();

            if (studentId.HasValue)
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                classes = await _context.Classes
                    .AsNoTracking()
                    .Where(c => c.Status == ClassStatus.Open && c.StartDate > today)
                    .Include(c => c.Subject)
                    .Include(c => c.Teacher)
                    .OrderBy(c => c.StartDate)
                    .ToListAsync();

                classOptions = (await BuildClassOptionsAsync(classes, studentId))
                    .Where(option => !option.AlreadyConfirmed)
                    .ToList();

                var availableClassIds = classOptions
                    .Select(option => option.ClassId)
                    .ToHashSet();
                classes = classes
                    .Where(classItem => availableClassIds.Contains(classItem.Id))
                    .ToList();

                var validSubjectIds = classes.Select(c => c.SubjectId).ToHashSet();
                if (subjectId.HasValue && !validSubjectIds.Contains(subjectId.Value))
                    subjectId = null;
            }
            else
            {
                subjectId = null;
            }

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
            if (!studentId.HasValue)
                return RedirectToAction(nameof(Index));

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

            return RedirectToAction(nameof(Confirmation), new { id = result.Enrollment!.Id });
        }

        // GET: /Enrollment/History
        [HttpGet]
        public async Task<IActionResult> History(
            int? studentId,
            EnrollmentStatus? status)
        {
            var parent = await GetCurrentParentAsync();
            if (parent == null)
                return Forbid();

            var students = await GetLinkedStudentsAsync(parent.Id);
            if (studentId.HasValue && students.All(student => student.Id != studentId.Value))
                studentId = null;

            if (status.HasValue && !Enum.IsDefined(status.Value))
                status = null;

            var query = _context.Enrollments
                .AsNoTracking()
                .Where(enrollment => enrollment.RegisteredByParentId == parent.Id)
                .Include(enrollment => enrollment.Student)
                .Include(enrollment => enrollment.Class)
                    .ThenInclude(classItem => classItem.Subject)
                .AsQueryable();

            if (studentId.HasValue)
                query = query.Where(enrollment => enrollment.StudentId == studentId.Value);

            if (status.HasValue)
                query = query.Where(enrollment => enrollment.Status == status.Value);

            var items = await query
                .OrderByDescending(enrollment => enrollment.CreatedAt)
                .Select(enrollment => new EnrollmentHistoryItemViewModel
                {
                    EnrollmentId = enrollment.Id,
                    StudentId = enrollment.StudentId,
                    StudentName = enrollment.Student.FullName,
                    ClassName = enrollment.Class.Name,
                    SubjectName = enrollment.Class.Subject.Name,
                    Fee = enrollment.Class.Fee,
                    Status = enrollment.Status,
                    CreatedAt = enrollment.CreatedAt,
                    ReservedUntil = enrollment.ReservedUntil,
                    ConfirmedAt = enrollment.ConfirmedAt
                })
                .ToListAsync();

            return View(new EnrollmentHistoryViewModel
            {
                Students = students,
                SelectedStudentId = studentId,
                SelectedStatus = status,
                Items = items
            });
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

            var activeEnrollmentsByClass = new Dictionary<int, (int Id, EnrollmentStatus Status)>();
            if (studentId.HasValue)
            {
                var activeEnrollments = await _context.Enrollments
                    .AsNoTracking()
                    .Where(enrollment => enrollment.StudentId == studentId.Value &&
                        classIds.Contains(enrollment.ClassId) &&
                        (enrollment.Status == EnrollmentStatus.Confirm ||
                         (enrollment.Status == EnrollmentStatus.PendingPayment && enrollment.ReservedUntil > now)))
                    .OrderByDescending(enrollment => enrollment.CreatedAt)
                    .Select(enrollment => new
                    {
                        enrollment.Id,
                        enrollment.ClassId,
                        enrollment.Status
                    })
                    .ToListAsync();

                activeEnrollmentsByClass = activeEnrollments
                    .GroupBy(enrollment => enrollment.ClassId)
                    .ToDictionary(
                        group => group.Key,
                        group =>
                        {
                            var enrollment = group.First();
                            return (enrollment.Id, enrollment.Status);
                        });
            }

            return classes.Select(classItem =>
            {
                heldByClass.TryGetValue(classItem.Id, out var held);
                activeEnrollmentsByClass.TryGetValue(
                    classItem.Id,
                    out var activeEnrollment);
                var hasActiveEnrollment = activeEnrollment.Id > 0;
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
                    AlreadyRegistered = hasActiveEnrollment,
                    ActiveEnrollmentId = hasActiveEnrollment
                        ? activeEnrollment.Id
                        : null,
                    ActiveEnrollmentStatus = hasActiveEnrollment
                        ? activeEnrollment.Status
                        : null,
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

    }
}
