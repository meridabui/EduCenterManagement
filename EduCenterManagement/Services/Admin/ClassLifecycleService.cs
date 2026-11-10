using System.Data;
using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Services.Admin;

public sealed record ClassTransitionResult(bool Success, string Message);

public sealed class ClassLifecycleService(ApplicationDbContext context, ClassSessionService sessions)
{
    public async Task<ClassTransitionResult> ChangeAsync(int id, ClassStatus target)
    {
        var source = target switch
        {
            ClassStatus.Open => ClassStatus.Draft,
            ClassStatus.InProgress => ClassStatus.Open,
            ClassStatus.Completed => ClassStatus.InProgress,
            ClassStatus.Cancelled => ClassStatus.Open,
            _ => (ClassStatus?)null
        };
        if (source == null) return new(false, "Không được chuyển về trạng thái trước đó.");

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        // Acquire a write lock before validation. Registration and draft edits lock this same row.
        var claimed = await context.Classes.Where(c => c.Id == id
                && (c.Status == source.Value || (target == ClassStatus.Cancelled && c.Status == ClassStatus.Draft)))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UpdatedAt, c => c.UpdatedAt));
        if (claimed == 0) return new(false, "Lớp không tồn tại hoặc không được phép chuyển trạng thái này. Vui lòng tải lại trang.");

        var cls = await context.Classes.Include(c => c.ClassSchedules).ThenInclude(s => s.TimeSlot)
            .SingleAsync(c => c.Id == id);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        if (target == ClassStatus.Open)
        {
            if (!await context.Subjects.AnyAsync(s => s.Id == cls.SubjectId && s.Status == SubjectStatus.Active))
                return new(false, "Môn học phải đang hoạt động trước khi mở đăng ký.");
            if (!cls.TeacherId.HasValue || !await context.TeacherProfiles.AnyAsync(t => t.Id == cls.TeacherId
                && t.User.Status == UserStatus.Active && t.User.Role == UserRole.Teacher
                && t.TeacherSubjects.Any(s => s.SubjectId == cls.SubjectId)))
                return new(false, "Phải phân công giáo viên đang hoạt động và đúng môn trước khi mở đăng ký.");
            if (cls.StartDate <= today || cls.EndDate <= cls.StartDate)
                return new(false, "Ngày bắt đầu phải sau hôm nay (giờ Việt Nam) và ngày kết thúc phải sau ngày bắt đầu.");
            if (cls.MaxStudents <= 0 || cls.Fee < 0)
                return new(false, "Sĩ số tối đa phải lớn hơn 0 và học phí không được âm.");

            var schedules = cls.ClassSchedules.ToList();
            if (schedules.Count == 0 || schedules.Select(s => s.DayOfWeek).Distinct().Count() != schedules.Count
                || schedules.Any(s => s.DayOfWeek < 2 || s.DayOfWeek > 8 || s.TimeSlot == null
                    || s.TimeSlot.Status != TimeSlotStatus.Active || s.TimeSlot.StartTime >= s.TimeSlot.EndTime))
                return new(false, "Lịch học phải hợp lệ, không lặp thứ và sử dụng khung giờ đang hoạt động.");

            var otherSchedules = await context.ClassSchedules.AsNoTracking().Include(s => s.TimeSlot)
                .Where(s => s.ClassId != id && s.Class.TeacherId == cls.TeacherId
                    && s.Class.Status != ClassStatus.Cancelled && s.Class.Status != ClassStatus.Completed
                    && s.Class.StartDate <= cls.EndDate && s.Class.EndDate >= cls.StartDate).ToListAsync();
            if (schedules.Any(a => otherSchedules.Any(b => a.DayOfWeek == b.DayOfWeek
                && a.TimeSlot.StartTime < b.TimeSlot.EndTime && b.TimeSlot.StartTime < a.TimeSlot.EndTime)))
                return new(false, "Giáo viên bị trùng lịch với lớp khác. Vui lòng sửa lịch trước khi mở đăng ký.");

            var generated = await sessions.SynchronizeAsync(cls, schedules, onlyIfEmpty: true);
            if (generated.Total == 0)
                return new(false, "Lịch học không có buổi nào trong khoảng thời gian của lớp.");
        }
        else if (target == ClassStatus.InProgress)
        {
            if (today < cls.StartDate || today > cls.EndDate)
                return new(false, "Chỉ bắt đầu lớp từ ngày khai giảng đến hết ngày kết thúc.");
            if (!await context.Enrollments.AnyAsync(e => e.ClassId == id && e.Status == EnrollmentStatus.Confirm))
                return new(false, "Lớp phải có ít nhất một đăng ký đã thanh toán trước khi bắt đầu.");
            if (!await context.ClassSessions.AnyAsync(s => s.ClassId == id))
                return new(false, "Lớp chưa có buổi học. Vui lòng tạo buổi học trước khi bắt đầu.");
        }
        else if (target == ClassStatus.Completed)
        {
            if (!await context.ClassSessions.AnyAsync(s => s.ClassId == id)
                || await context.ClassSessions.AnyAsync(s => s.ClassId == id
                    && s.Status != ClassSessionStatus.Completed && s.Status != ClassSessionStatus.Cancelled))
                return new(false, "Chỉ hoàn thành lớp khi đã có buổi học và tất cả buổi đã hoàn thành hoặc hủy.");
        }
        else if (target == ClassStatus.Cancelled)
        {
            if (await context.Enrollments.AnyAsync(e => e.ClassId == id && e.Status == EnrollmentStatus.Confirm)
                || await context.Payments.AnyAsync(p => p.Enrollment.ClassId == id && p.Status == PaymentStatus.Paid))
                return new(false, "Không thể hủy lớp có đăng ký đã thanh toán. Cần xử lý hoàn tiền trước.");
            var now = DateTime.UtcNow;
            var recipientIds = await context.Enrollments
                .Where(e => e.ClassId == id && e.Status == EnrollmentStatus.PendingPayment)
                .Select(e => e.RegisteredByParent.UserId)
                .Union(context.Enrollments.Where(e => e.ClassId == id && e.Status == EnrollmentStatus.PendingPayment)
                    .Select(e => e.Student.UserId))
                .Union(context.TeacherProfiles.Where(t => t.Id == cls.TeacherId).Select(t => t.UserId))
                .ToListAsync();
            context.Notifications.AddRange(recipientIds.Select(userId => new Notification
            {
                UserId = userId,
                Type = "ClassCancelled",
                Title = "Lớp học đã hủy",
                Message = $"Lớp {cls.Name} đã bị hủy. Các đăng ký đang chờ thanh toán của lớp đã được hủy.",
                ReferenceType = "Class",
                ReferenceId = id,
                CreatedAt = now
            }));
            await context.Enrollments.Where(e => e.ClassId == id && e.Status == EnrollmentStatus.PendingPayment)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, EnrollmentStatus.Cancelled)
                    .SetProperty(e => e.CancelledAt, (DateTime?)now).SetProperty(e => e.UpdatedAt, now));
            await context.ClassSessions.Where(s => s.ClassId == id && s.Status != ClassSessionStatus.Completed
                && s.Status != ClassSessionStatus.Cancelled)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, ClassSessionStatus.Cancelled)
                    .SetProperty(e => e.UpdatedAt, now));
        }

        cls.Status = target;
        cls.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return new(true, "Đã cập nhật thành công.");
    }
}
