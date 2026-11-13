using EduCenterManagement.Enums;
using EduCenterManagement.Models;

namespace EduCenterManagement.Repositories.Enrollments;

// Đăng ký còn hiệu lực của một học sinh (đã xác nhận, hoặc đang giữ chỗ).
public sealed record ActiveEnrollmentInfo(
    int Id,
    int ClassId,
    EnrollmentStatus Status);

// Transaction của luồng đăng ký; không Commit thì sẽ tự Rollback khi Dispose.
public interface IEnrollmentTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}

public interface IEnrollmentRepository
{
    Task<ParentProfile?> GetParentByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default);

    // Học sinh đã được Parent chấp nhận liên kết, sắp xếp theo tên.
    Task<List<StudentProfile>> GetLinkedStudentsAsync(
        int parentId,
        CancellationToken cancellationToken = default);

    // Lớp đang mở đăng ký và chưa bắt đầu (kèm Subject, Teacher).
    Task<List<Class>> GetOpenClassesAsync(
        DateOnly today,
        CancellationToken cancellationToken = default);

    Task<Class?> GetOpenClassAsync(
        int classId,
        DateOnly today,
        CancellationToken cancellationToken = default);

    Task<List<ClassSchedule>> GetSchedulesAsync(
        IReadOnlyCollection<int> classIds,
        CancellationToken cancellationToken = default);

    // Số chỗ đang bị giữ theo lớp (Confirm + Pending còn hạn giữ chỗ).
    Task<Dictionary<int, int>> GetHeldSeatsByClassAsync(
        IReadOnlyCollection<int> classIds,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    // Mới nhất trước.
    Task<List<ActiveEnrollmentInfo>> GetActiveEnrollmentsAsync(
        int studentId,
        IReadOnlyCollection<int> classIds,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<List<Enrollment>> GetHistoryAsync(
        int parentId,
        int? studentId,
        EnrollmentStatus? status,
        CancellationToken cancellationToken = default);

    Task<Enrollment?> GetEnrollmentForParentAsync(
        int enrollmentId,
        int parentId,
        CancellationToken cancellationToken = default);

    // ================= PHÍA GHI (RegisterAsync, ExpireOverdueAsync) =================

    // BR13: Parent đã được Student chấp nhận liên kết.
    Task<bool> IsParentLinkedToStudentAsync(
        int parentId,
        int studentId,
        CancellationToken cancellationToken = default);

    Task<IEnrollmentTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default);

    // Đọc lớp kèm khóa UPDLOCK, ROWLOCK. Chỉ gọi trong transaction.
    Task<Class?> GetClassForUpdateAsync(
        int classId,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveEnrollmentAsync(
        int studentId,
        int classId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<int> CountHeldSeatsAsync(
        int classId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    // Lịch của một lớp (kèm TimeSlot), không sắp xếp.
    Task<List<ClassSchedule>> GetSchedulesForClassAsync(
        int classId,
        CancellationToken cancellationToken = default);

    // Lịch các lớp khác của học sinh có thời gian học giao với [classStart, classEnd].
    Task<List<ClassSchedule>> GetOverlappingSchedulesAsync(
        int studentId,
        int classId,
        DateOnly classStart,
        DateOnly classEnd,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<string?> GetStudentNameAsync(
        int studentId,
        CancellationToken cancellationToken = default);

    void AddEnrollment(Enrollment enrollment);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    // BR16: một câu UPDATE có điều kiện Pending quá hạn -> Cancelled.
    Task<int> CancelOverdueAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
