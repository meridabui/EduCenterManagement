using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.Repositories.Enrollments;
using EduCenterManagement.ViewModels.Registration;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Services
{
    public class EnrollmentResult
    {
        public bool Success { get; private set; }
        public string? Error { get; private set; }
        public Enrollment? Enrollment { get; private set; }
        public string ClassName { get; private set; } = string.Empty;
        public string StudentName { get; private set; } = string.Empty;

        public static EnrollmentResult Fail(string error)
        {
            return new EnrollmentResult { Success = false, Error = error };
        }

        public static EnrollmentResult Ok(
            Enrollment enrollment,
            string className,
            string studentName)
        {
            return new EnrollmentResult
            {
                Success = true,
                Enrollment = enrollment,
                ClassName = className,
                StudentName = studentName
            };
        }
    }

    public enum EnrollmentReadStatus
    {
        Success,
        Forbidden,
        NotFound,
        NoStudentSelected
    }

    public sealed record EnrollmentReadResult<T>(
        EnrollmentReadStatus Status,
        T? Data = null) where T : class;

    public interface IEnrollmentService
    {
        // Kiểm tra BR13, BR14 và tạo Enrollment Pending giữ chỗ (BR15).
        Task<EnrollmentResult> RegisterAsync(int parentId, int studentId, int classId);

        // BR16: hủy các Enrollment Pending đã quá hạn giữ chỗ.
        Task<int> ExpireOverdueAsync(CancellationToken cancellationToken = default);

        // Id ParentProfile của tài khoản đang đăng nhập; null nếu không phải Parent.
        Task<int?> GetParentIdAsync(
            int parentUserId,
            CancellationToken cancellationToken = default);

        Task<EnrollmentReadResult<EnrollmentIndexViewModel>> GetIndexAsync(
            int parentUserId,
            int? studentId,
            int? subjectId,
            CancellationToken cancellationToken = default);

        Task<EnrollmentReadResult<EnrollmentClassDetailsViewModel>> GetClassDetailsAsync(
            int parentUserId,
            int classId,
            int? studentId,
            CancellationToken cancellationToken = default);

        Task<EnrollmentReadResult<EnrollmentHistoryViewModel>> GetHistoryAsync(
            int parentUserId,
            int? studentId,
            EnrollmentStatus? status,
            CancellationToken cancellationToken = default);

        Task<EnrollmentReadResult<EnrollmentConfirmationViewModel>> GetConfirmationAsync(
            int parentUserId,
            int enrollmentId,
            CancellationToken cancellationToken = default);
    }

    public class EnrollmentService : IEnrollmentService
    {
        // BR16: thời gian giữ chỗ tạm thời.
        public const int HoldMinutes = 15;

        private readonly IEnrollmentRepository _repository;
        private readonly TimeProvider _clock;
        private readonly ILogger<EnrollmentService> _logger;

        public EnrollmentService(
            IEnrollmentRepository repository,
            TimeProvider clock,
            ILogger<EnrollmentService> logger)
        {
            _repository = repository;
            _clock = clock;
            _logger = logger;
        }

        public async Task<EnrollmentResult> RegisterAsync(
            int parentId,
            int studentId,
            int classId)
        {
            var now = UtcNow();

            // BR13: chỉ đăng ký cho Student đã được Parent chấp nhận liên kết.
            var isLinked = await _repository.IsParentLinkedToStudentAsync(parentId, studentId);

            if (!isLinked)
            {
                return EnrollmentResult.Fail(
                    "Bạn chỉ có thể đăng ký lớp cho học sinh đã liên kết với bạn.");
            }

            await using var transaction = await _repository.BeginTransactionAsync();

            // Khóa dòng Class để các lượt đăng ký cùng một lớp được xử lý lần lượt,
            // tránh hai Parent cùng giữ chỗ cuối cùng và làm vượt sĩ số.
            var classItem = await _repository.GetClassForUpdateAsync(classId);

            if (classItem == null)
            {
                return EnrollmentResult.Fail("Lớp học không tồn tại.");
            }

            // BR14 (1): lớp chưa bắt đầu và đang mở đăng ký.
            if (classItem.Status != ClassStatus.Open)
            {
                return EnrollmentResult.Fail("Lớp học hiện không mở đăng ký.");
            }

            var today = LocalToday();

            if (classItem.StartDate <= today)
            {
                return EnrollmentResult.Fail("Lớp học đã bắt đầu, không thể đăng ký.");
            }

            // BR14 (5): không đăng ký trùng.
            if (await _repository.HasActiveEnrollmentAsync(studentId, classId, now))
            {
                return EnrollmentResult.Fail(
                    "Học sinh đã đăng ký lớp này (đã thanh toán hoặc đang giữ chỗ).");
            }

            // BR14 (2), BR17: còn chỗ trống.
            var heldSeats = await _repository.CountHeldSeatsAsync(classId, now);

            if (heldSeats >= classItem.MaxStudents)
            {
                return EnrollmentResult.Fail("Lớp học đã hết chỗ.");
            }

            // BR14 (3): không trùng lịch với các lớp khác của học sinh.
            var targetSchedules = await _repository.GetSchedulesForClassAsync(classId);

            var classStart = classItem.StartDate;
            var classEnd = classItem.EndDate;

            var otherSchedules = await _repository.GetOverlappingSchedulesAsync(
                studentId,
                classId,
                classStart,
                classEnd,
                now);

            foreach (var target in targetSchedules)
            {
                foreach (var other in otherSchedules)
                {
                    var sameDay = target.DayOfWeek == other.DayOfWeek;

                    var overlapTime =
                        target.TimeSlot.StartTime < other.TimeSlot.EndTime &&
                        other.TimeSlot.StartTime < target.TimeSlot.EndTime;

                    if (sameDay && overlapTime)
                    {
                        return EnrollmentResult.Fail(
                            $"Trùng lịch với lớp \"{other.Class.Name}\" " +
                            $"({DayLabel(other.DayOfWeek)}, {other.TimeSlot.Name} " +
                            $"{other.TimeSlot.StartTime:HH:mm}-{other.TimeSlot.EndTime:HH:mm}).");
                    }
                }
            }

            var studentName = await _repository.GetStudentNameAsync(studentId) ?? string.Empty;

            // BR15: tạo Enrollment Pending và giữ chỗ tạm thời.
            var enrollment = new Enrollment
            {
                StudentId = studentId,
                ClassId = classId,
                RegisteredByParentId = parentId,
                Status = EnrollmentStatus.PendingPayment,
                ReservedUntil = now.AddMinutes(HoldMinutes),
                CreatedAt = now,
                UpdatedAt = now
            };

            _repository.AddEnrollment(enrollment);

            try
            {
                await _repository.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Create enrollment failed (parent {ParentId}, student {StudentId}, class {ClassId})",
                    parentId, studentId, classId);

                return EnrollmentResult.Fail("Không thể tạo đăng ký. Vui lòng thử lại.");
            }

            return EnrollmentResult.Ok(enrollment, classItem.Name, studentName);
        }

        public async Task<int> ExpireOverdueAsync(CancellationToken cancellationToken = default)
        {
            // Một câu UPDATE có điều kiện Status = Pending nên không ghi đè
            // Enrollment vừa được chuyển sang Paid ở luồng thanh toán.
            return await _repository.CancelOverdueAsync(UtcNow(), cancellationToken);
        }

        // ================= PHÍA ĐỌC (dùng cho EnrollmentController) =================

        public async Task<int?> GetParentIdAsync(
            int parentUserId,
            CancellationToken cancellationToken = default)
        {
            var parent = await _repository.GetParentByUserIdAsync(
                parentUserId,
                cancellationToken);

            return parent?.Id;
        }

        public async Task<EnrollmentReadResult<EnrollmentIndexViewModel>> GetIndexAsync(
            int parentUserId,
            int? studentId,
            int? subjectId,
            CancellationToken cancellationToken = default)
        {
            var parentId = await GetParentIdAsync(parentUserId, cancellationToken);

            if (parentId == null)
            {
                return new(EnrollmentReadStatus.Forbidden);
            }

            var students = await GetLinkedStudentsAsync(parentId.Value, cancellationToken);

            // Chỉ chấp nhận học sinh thuộc danh sách đã liên kết.
            studentId = ResolveStudentId(students, studentId, autoSelectSingle: true);

            var classes = new List<Class>();
            var classOptions = new List<ClassOptionViewModel>();

            if (studentId.HasValue)
            {
                var today = LocalToday();
                classes = await _repository.GetOpenClassesAsync(today, cancellationToken);

                classOptions = (await BuildClassOptionsAsync(classes, studentId, cancellationToken))
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

            return new(EnrollmentReadStatus.Success, model);
        }

        public async Task<EnrollmentReadResult<EnrollmentClassDetailsViewModel>> GetClassDetailsAsync(
            int parentUserId,
            int classId,
            int? studentId,
            CancellationToken cancellationToken = default)
        {
            var parentId = await GetParentIdAsync(parentUserId, cancellationToken);

            if (parentId == null)
            {
                return new(EnrollmentReadStatus.Forbidden);
            }

            var students = await GetLinkedStudentsAsync(parentId.Value, cancellationToken);
            studentId = ResolveStudentId(students, studentId, autoSelectSingle: true);

            if (!studentId.HasValue)
            {
                return new(EnrollmentReadStatus.NoStudentSelected);
            }

            var classItem = await _repository.GetOpenClassAsync(
                classId,
                LocalToday(),
                cancellationToken);

            if (classItem == null)
            {
                return new(EnrollmentReadStatus.NotFound);
            }

            var option = (await BuildClassOptionsAsync(
                new List<Class> { classItem },
                studentId,
                cancellationToken)).Single();

            return new(EnrollmentReadStatus.Success, new EnrollmentClassDetailsViewModel
            {
                Students = students,
                SelectedStudentId = studentId,
                Class = option
            });
        }

        public async Task<EnrollmentReadResult<EnrollmentHistoryViewModel>> GetHistoryAsync(
            int parentUserId,
            int? studentId,
            EnrollmentStatus? status,
            CancellationToken cancellationToken = default)
        {
            var parentId = await GetParentIdAsync(parentUserId, cancellationToken);

            if (parentId == null)
            {
                return new(EnrollmentReadStatus.Forbidden);
            }

            var students = await GetLinkedStudentsAsync(parentId.Value, cancellationToken);
            studentId = ResolveStudentId(students, studentId, autoSelectSingle: false);

            if (status.HasValue && !Enum.IsDefined(status.Value))
                status = null;

            var enrollments = await _repository.GetHistoryAsync(
                parentId.Value,
                studentId,
                status,
                cancellationToken);

            var items = enrollments
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
                .ToList();

            return new(EnrollmentReadStatus.Success, new EnrollmentHistoryViewModel
            {
                Students = students,
                SelectedStudentId = studentId,
                SelectedStatus = status,
                Items = items
            });
        }

        public async Task<EnrollmentReadResult<EnrollmentConfirmationViewModel>> GetConfirmationAsync(
            int parentUserId,
            int enrollmentId,
            CancellationToken cancellationToken = default)
        {
            var parentId = await GetParentIdAsync(parentUserId, cancellationToken);

            if (parentId == null)
            {
                return new(EnrollmentReadStatus.Forbidden);
            }

            var enrollment = await _repository.GetEnrollmentForParentAsync(
                enrollmentId,
                parentId.Value,
                cancellationToken);

            if (enrollment == null)
            {
                return new(EnrollmentReadStatus.NotFound);
            }

            var reservedUntil = enrollment.ReservedUntil.HasValue
                ? DateTime.SpecifyKind(enrollment.ReservedUntil.Value, DateTimeKind.Utc)
                : (DateTime?)null;

            return new(EnrollmentReadStatus.Success, new EnrollmentConfirmationViewModel
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
                               reservedUntil.Value > UtcNow()
            });
        }

        private DateTime UtcNow() => _clock.GetUtcNow().UtcDateTime;

        // Giữ hành vi cũ: "hôm nay" tính theo giờ máy chủ (như DateTime.Now).
        private DateOnly LocalToday() =>
            DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);

        private static int? ResolveStudentId(
            IReadOnlyList<StudentOptionViewModel> students,
            int? studentId,
            bool autoSelectSingle)
        {
            if (studentId.HasValue && students.All(s => s.Id != studentId.Value))
                studentId = null;

            if (!studentId.HasValue && autoSelectSingle && students.Count == 1)
                studentId = students[0].Id;

            return studentId;
        }

        private async Task<List<StudentOptionViewModel>> GetLinkedStudentsAsync(
            int parentId,
            CancellationToken cancellationToken)
        {
            var students = await _repository.GetLinkedStudentsAsync(
                parentId,
                cancellationToken);

            return students
                .Select(student => new StudentOptionViewModel
                {
                    Id = student.Id,
                    FullName = student.FullName
                })
                .ToList();
        }

        private async Task<List<ClassOptionViewModel>> BuildClassOptionsAsync(
            IReadOnlyCollection<Class> classes,
            int? studentId,
            CancellationToken cancellationToken)
        {
            if (classes.Count == 0)
                return new List<ClassOptionViewModel>();

            var now = UtcNow();
            var classIds = classes.Select(c => c.Id).ToList();

            var schedules = await _repository.GetSchedulesAsync(classIds, cancellationToken);
            var heldByClass = await _repository.GetHeldSeatsByClassAsync(
                classIds,
                now,
                cancellationToken);

            var activeEnrollmentsByClass = new Dictionary<int, ActiveEnrollmentInfo>();
            if (studentId.HasValue)
            {
                var activeEnrollments = await _repository.GetActiveEnrollmentsAsync(
                    studentId.Value,
                    classIds,
                    now,
                    cancellationToken);

                // Danh sách đã sắp xếp mới nhất trước nên lấy phần tử đầu mỗi lớp.
                activeEnrollmentsByClass = activeEnrollments
                    .GroupBy(enrollment => enrollment.ClassId)
                    .ToDictionary(group => group.Key, group => group.First());
            }

            return classes.Select(classItem =>
            {
                heldByClass.TryGetValue(classItem.Id, out var held);
                activeEnrollmentsByClass.TryGetValue(classItem.Id, out var activeEnrollment);

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
                    AlreadyRegistered = activeEnrollment != null,
                    ActiveEnrollmentId = activeEnrollment?.Id,
                    ActiveEnrollmentStatus = activeEnrollment?.Status,
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

        private static string DayLabel(int dayOfWeek)
        {
            return dayOfWeek == 8 ? "Chủ nhật" : $"Thứ {dayOfWeek}";
        }
    }
}
