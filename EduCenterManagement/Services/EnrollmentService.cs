using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
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

    public interface IEnrollmentService
    {
        // Kiểm tra BR13, BR14 và tạo Enrollment Pending giữ chỗ (BR15).
        Task<EnrollmentResult> RegisterAsync(int parentId, int studentId, int classId);

        // BR16: hủy các Enrollment Pending đã quá hạn giữ chỗ.
        Task<int> ExpireOverdueAsync(CancellationToken cancellationToken = default);
    }

    public class EnrollmentService : IEnrollmentService
    {
        // BR16: thời gian giữ chỗ tạm thời.
        public const int HoldMinutes = 15;

        private readonly ApplicationDbContext _context;
        private readonly ILogger<EnrollmentService> _logger;

        public EnrollmentService(
            ApplicationDbContext context,
            ILogger<EnrollmentService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<EnrollmentResult> RegisterAsync(
            int parentId,
            int studentId,
            int classId)
        {
            var now = DateTime.UtcNow;

            // BR13: chỉ đăng ký cho Student đã được Parent chấp nhận liên kết.
            var isLinked = await _context.ParentStudentLinks.AnyAsync(l =>
                l.ParentId == parentId &&
                l.StudentId == studentId &&
                l.Status == ParentStudentLinkStatus.Accepted);

            if (!isLinked)
            {
                return EnrollmentResult.Fail(
                    "Bạn chỉ có thể đăng ký lớp cho học sinh đã liên kết với bạn.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            // Khóa dòng Class để các lượt đăng ký cùng một lớp được xử lý lần lượt,
            // tránh hai Parent cùng giữ chỗ cuối cùng và làm vượt sĩ số.
            var classItem = await _context.Classes
                .FromSqlInterpolated(
                    $"SELECT * FROM classes WITH (UPDLOCK, ROWLOCK) WHERE Id = {classId}")
                .SingleOrDefaultAsync();

            if (classItem == null)
            {
                return EnrollmentResult.Fail("Lớp học không tồn tại.");
            }

            // BR14 (1): lớp chưa bắt đầu và đang mở đăng ký.
            if (classItem.Status != ClassStatus.Open)
            {
                return EnrollmentResult.Fail("Lớp học hiện không mở đăng ký.");
            }

            var today = DateOnly.FromDateTime(DateTime.Now);

            if (classItem.StartDate <= today)
            {
                return EnrollmentResult.Fail("Lớp học đã bắt đầu, không thể đăng ký.");
            }

            // Enrollment còn hiệu lực = Paid, hoặc Pending còn trong thời gian giữ chỗ (BR17).
            var activeForStudent = _context.Enrollments.Where(e =>
                e.StudentId == studentId &&
                (e.Status == EnrollmentStatus.Paid ||
                 (e.Status == EnrollmentStatus.Pending && e.ReservedUntil > now)));

            // BR14 (5): không đăng ký trùng.
            if (await activeForStudent.AnyAsync(e => e.ClassId == classId))
            {
                return EnrollmentResult.Fail(
                    "Học sinh đã đăng ký lớp này (đã thanh toán hoặc đang giữ chỗ).");
            }

            // BR14 (2), BR17: còn chỗ trống.
            var heldSeats = await _context.Enrollments.CountAsync(e =>
                e.ClassId == classId &&
                (e.Status == EnrollmentStatus.Paid ||
                 (e.Status == EnrollmentStatus.Pending && e.ReservedUntil > now)));

            if (heldSeats >= classItem.MaxStudents)
            {
                return EnrollmentResult.Fail("Lớp học đã hết chỗ.");
            }

            // BR14 (3): không trùng lịch với các lớp khác của học sinh.
            var targetSchedules = await _context.ClassSchedules
                .AsNoTracking()
                .Include(s => s.TimeSlot)
                .Where(s => s.ClassId == classId)
                .ToListAsync();

            var classStart = classItem.StartDate;
            var classEnd = classItem.EndDate;

            var otherSchedules = await _context.ClassSchedules
                .AsNoTracking()
                .Include(s => s.TimeSlot)
                .Include(s => s.Class)
                .Where(s =>
                    s.ClassId != classId &&
                    s.Class.Status != ClassStatus.Cancelled &&
                    s.Class.Status != ClassStatus.Completed &&
                    s.Class.StartDate <= classEnd &&
                    s.Class.EndDate >= classStart &&
                    _context.Enrollments.Any(e =>
                        e.StudentId == studentId &&
                        e.ClassId == s.ClassId &&
                        (e.Status == EnrollmentStatus.Paid ||
                         (e.Status == EnrollmentStatus.Pending && e.ReservedUntil > now))))
                .ToListAsync();

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

            // BR14 (4): điều kiện tiên quyết (nếu có).
            if (classItem.PrerequisiteClassId is int prerequisiteId)
            {
                var completed = await _context.Enrollments.AnyAsync(e =>
                    e.StudentId == studentId &&
                    e.ClassId == prerequisiteId &&
                    e.Status == EnrollmentStatus.Paid &&
                    e.Class.Status == ClassStatus.Completed);

                if (!completed)
                {
                    var prerequisiteName = await _context.Classes
                        .AsNoTracking()
                        .Where(c => c.Id == prerequisiteId)
                        .Select(c => c.Name)
                        .FirstOrDefaultAsync() ?? "tiên quyết";

                    return EnrollmentResult.Fail(
                        $"Học sinh chưa hoàn thành lớp tiên quyết \"{prerequisiteName}\".");
                }
            }

            var studentName = await _context.StudentProfiles
                .AsNoTracking()
                .Where(s => s.Id == studentId)
                .Select(s => s.FullName)
                .FirstOrDefaultAsync() ?? string.Empty;

            // BR15: tạo Enrollment Pending và giữ chỗ tạm thời.
            var enrollment = new Enrollment
            {
                StudentId = studentId,
                ClassId = classId,
                RegisteredByParentId = parentId,
                Status = EnrollmentStatus.Pending,
                ReservedUntil = now.AddMinutes(HoldMinutes),
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Enrollments.Add(enrollment);

            try
            {
                await _context.SaveChangesAsync();
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
            var now = DateTime.UtcNow;
            var cancelledAt = (DateTime?)now;

            // Một câu UPDATE có điều kiện Status = Pending nên không ghi đè
            // Enrollment vừa được chuyển sang Paid ở luồng thanh toán.
            return await _context.Enrollments
                .Where(e =>
                    e.Status == EnrollmentStatus.Pending &&
                    e.ReservedUntil != null &&
                    e.ReservedUntil <= now)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(e => e.Status, EnrollmentStatus.Cancelled)
                        .SetProperty(e => e.CancelledAt, cancelledAt)
                        .SetProperty(e => e.UpdatedAt, now),
                    cancellationToken);
        }

        private static string DayLabel(int dayOfWeek)
        {
            return dayOfWeek == 8 ? "Chủ nhật" : $"Thứ {dayOfWeek}";
        }
    }
}
