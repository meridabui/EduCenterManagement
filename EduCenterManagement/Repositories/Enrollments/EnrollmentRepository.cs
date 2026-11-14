using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EduCenterManagement.Repositories.Enrollments;

public sealed class EnrollmentRepository : IEnrollmentRepository
{
    private readonly ApplicationDbContext _context;

    public EnrollmentRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<ParentProfile?> GetParentByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        return _context.ParentProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
    }

    public Task<List<StudentProfile>> GetLinkedStudentsAsync(
        int parentId,
        CancellationToken cancellationToken = default)
    {
        return _context.ParentStudentLinks
            .AsNoTracking()
            .Where(link => link.ParentId == parentId &&
                           link.Status == ParentStudentLinkStatus.Accepted)
            .OrderBy(link => link.Student.FullName)
            .Select(link => link.Student)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Class>> GetOpenClassesAsync(
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        return _context.Classes
            .AsNoTracking()
            .Where(c => c.Status == ClassStatus.Open && c.StartDate > today)
            .Include(c => c.Subject)
            .Include(c => c.Teacher)
            .OrderBy(c => c.StartDate)
            .ToListAsync(cancellationToken);
    }

    public Task<Class?> GetOpenClassAsync(
        int classId,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        return _context.Classes
            .AsNoTracking()
            .Where(c => c.Id == classId &&
                        c.Status == ClassStatus.Open &&
                        c.StartDate > today)
            .Include(c => c.Subject)
            .Include(c => c.Teacher)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<List<ClassSchedule>> GetSchedulesAsync(
        IReadOnlyCollection<int> classIds,
        CancellationToken cancellationToken = default)
    {
        return _context.ClassSchedules
            .AsNoTracking()
            .Where(schedule => classIds.Contains(schedule.ClassId))
            .Include(schedule => schedule.TimeSlot)
            .OrderBy(schedule => schedule.DayOfWeek)
            .ThenBy(schedule => schedule.TimeSlot.StartTime)
            .ToListAsync(cancellationToken);
    }

    public Task<Dictionary<int, int>> GetHeldSeatsByClassAsync(
        IReadOnlyCollection<int> classIds,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        return _context.Enrollments
            .AsNoTracking()
            .Where(enrollment => classIds.Contains(enrollment.ClassId) &&
                (enrollment.Status == EnrollmentStatus.Confirm ||
                 (enrollment.Status == EnrollmentStatus.PendingPayment &&
                  enrollment.ReservedUntil > nowUtc)))
            .GroupBy(enrollment => enrollment.ClassId)
            .Select(group => new { ClassId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(
                item => item.ClassId,
                item => item.Count,
                cancellationToken);
    }

    public Task<List<ActiveEnrollmentInfo>> GetActiveEnrollmentsAsync(
        int studentId,
        IReadOnlyCollection<int> classIds,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        return _context.Enrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.StudentId == studentId &&
                classIds.Contains(enrollment.ClassId) &&
                (enrollment.Status == EnrollmentStatus.Confirm ||
                 (enrollment.Status == EnrollmentStatus.PendingPayment &&
                  enrollment.ReservedUntil > nowUtc)))
            .OrderByDescending(enrollment => enrollment.CreatedAt)
            .Select(enrollment => new ActiveEnrollmentInfo(
                enrollment.Id,
                enrollment.ClassId,
                enrollment.Status))
            .ToListAsync(cancellationToken);
    }

    public Task<List<Enrollment>> GetHistoryAsync(
        int parentId,
        int? studentId,
        EnrollmentStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Enrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.RegisteredByParentId == parentId)
            .Include(enrollment => enrollment.Student)
            .Include(enrollment => enrollment.Class)
                .ThenInclude(classItem => classItem.Subject)
            .AsQueryable();

        if (studentId.HasValue)
            query = query.Where(enrollment => enrollment.StudentId == studentId.Value);

        if (status.HasValue)
            query = query.Where(enrollment => enrollment.Status == status.Value);

        return query
            .OrderByDescending(enrollment => enrollment.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Enrollment?> GetEnrollmentForParentAsync(
        int enrollmentId,
        int parentId,
        CancellationToken cancellationToken = default)
    {
        return _context.Enrollments
            .AsNoTracking()
            .Include(e => e.Student)
            .Include(e => e.Class)
                .ThenInclude(c => c.Subject)
            .SingleOrDefaultAsync(
                e => e.Id == enrollmentId && e.RegisteredByParentId == parentId,
                cancellationToken);
    }

    // ================= PHÍA GHI (RegisterAsync, ExpireOverdueAsync) =================

    public Task<bool> IsParentLinkedToStudentAsync(
        int parentId,
        int studentId,
        CancellationToken cancellationToken = default)
    {
        return _context.ParentStudentLinks.AnyAsync(l =>
            l.ParentId == parentId &&
            l.StudentId == studentId &&
            l.Status == ParentStudentLinkStatus.Accepted,
            cancellationToken);
    }

    public async Task<IEnrollmentTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        return new EnrollmentTransaction(transaction);
    }

    public Task<Class?> GetClassForUpdateAsync(
        int classId,
        CancellationToken cancellationToken = default)
    {
        // Khóa dòng Class để các lượt đăng ký cùng một lớp được xử lý lần lượt.
        return _context.Classes
            .FromSqlInterpolated(
                $"SELECT * FROM classes WITH (UPDLOCK, ROWLOCK) WHERE Id = {classId}")
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<bool> HasActiveEnrollmentAsync(
        int studentId,
        int classId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        // Enrollment còn hiệu lực = Paid, hoặc Pending còn trong thời gian giữ chỗ (BR17).
        return _context.Enrollments.AnyAsync(e =>
            e.StudentId == studentId &&
            e.ClassId == classId &&
            (e.Status == EnrollmentStatus.Confirm ||
             (e.Status == EnrollmentStatus.PendingPayment && e.ReservedUntil > nowUtc)),
            cancellationToken);
    }

    public Task<int> CountHeldSeatsAsync(
        int classId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        return _context.Enrollments.CountAsync(e =>
            e.ClassId == classId &&
            (e.Status == EnrollmentStatus.Confirm ||
             (e.Status == EnrollmentStatus.PendingPayment && e.ReservedUntil > nowUtc)),
            cancellationToken);
    }

    public Task<List<ClassSchedule>> GetSchedulesForClassAsync(
        int classId,
        CancellationToken cancellationToken = default)
    {
        return _context.ClassSchedules
            .AsNoTracking()
            .Include(s => s.TimeSlot)
            .Where(s => s.ClassId == classId)
            .ToListAsync(cancellationToken);
    }

    public Task<List<ClassSchedule>> GetOverlappingSchedulesAsync(
        int studentId,
        int classId,
        DateOnly classStart,
        DateOnly classEnd,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        return _context.ClassSchedules
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
                    (e.Status == EnrollmentStatus.Confirm ||
                     (e.Status == EnrollmentStatus.PendingPayment && e.ReservedUntil > nowUtc))))
            .ToListAsync(cancellationToken);
    }

    public Task<string?> GetStudentNameAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        return _context.StudentProfiles
            .AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => s.FullName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void AddEnrollment(Enrollment enrollment)
    {
        _context.Enrollments.Add(enrollment);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CancelOverdueAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var cancelledAt = (DateTime?)nowUtc;

        // Điều kiện Status = Pending nên không ghi đè Enrollment vừa chuyển sang Paid.
        return _context.Enrollments
            .Where(e =>
                e.Status == EnrollmentStatus.PendingPayment &&
                e.ReservedUntil != null &&
                e.ReservedUntil <= nowUtc)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(e => e.Status, EnrollmentStatus.Cancelled)
                    .SetProperty(e => e.CancelledAt, cancelledAt)
                    .SetProperty(e => e.UpdatedAt, nowUtc),
                cancellationToken);
    }

    private sealed class EnrollmentTransaction : IEnrollmentTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public EnrollmentTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return _transaction.CommitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return _transaction.DisposeAsync();
        }
    }
}
