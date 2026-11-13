using System.Data;
using EduCenterManagement.Repositories.Admin;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;

namespace EduCenterManagement.Services.Admin;

public sealed record ClassTransitionResult(bool Success, string Message);

public sealed class ClassLifecycleService(IAdminClassRepository repository, IAdminUnitOfWork unitOfWork, IClassSessionService sessions) : IClassLifecycleService
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

        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        // Acquire a write lock before validation. Registration and draft edits lock this same row.
        var claimed = await repository.LockForTransitionAsync(id, source.Value, target);
        if (claimed == 0) return new(false, "Lớp không tồn tại hoặc không được phép chuyển trạng thái này. Vui lòng tải lại trang.");

        var cls = (await repository.GetWithSchedulesAsync(id, tracking: true, includeSlots: true))!;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        if (target == ClassStatus.Open)
        {
            if (!await repository.IsSubjectActiveAsync(cls.SubjectId))
                return new(false, "Môn học phải đang hoạt động trước khi mở đăng ký.");
            if (!cls.TeacherId.HasValue || !await repository.IsActiveTeacherForSubjectAsync(cls.TeacherId.Value, cls.SubjectId))
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

            var otherSchedules = await repository.GetOtherSchedulesAsync(id, cls.TeacherId, cls.StartDate, cls.EndDate);
            if (schedules.Any(a => otherSchedules.Any(b => a.DayOfWeek == b.DayOfWeek
                && a.TimeSlot.StartTime < b.TimeSlot.EndTime && b.TimeSlot.StartTime < a.TimeSlot.EndTime)))
                return new(false, "Giáo viên bị trùng lịch với lớp khác. Vui lòng sửa lịch trước khi mở đăng ký.");

            var generated = await sessions.SynchronizeAsync(cls, schedules, onlyIfEmpty: true);
            if (generated.Total == 0)
                return new(false, "Lịch học không có buổi nào trong khoảng thời gian của lớp.");
        }
        else if (target == ClassStatus.InProgress)
        {
            if (!await repository.HasConfirmedEnrollmentsAsync(id))
                return new(false, "Lớp phải có ít nhất một đăng ký đã thanh toán trước khi bắt đầu.");
        }
        else if (target == ClassStatus.Completed)
        {
            if (!await repository.HasSessionsAsync(id)
                || await repository.HasUnfinishedSessionsAsync(id))
                return new(false, "Chỉ hoàn thành lớp khi đã có buổi học và tất cả buổi đã hoàn thành hoặc hủy.");
        }
        else if (target == ClassStatus.Cancelled)
        {
            if (await repository.HasConfirmedEnrollmentsAsync(id)
                || await repository.HasPaidPaymentsAsync(id))
                return new(false, "Không thể hủy lớp có đăng ký đã thanh toán. Cần xử lý hoàn tiền trước.");
            var now = DateTime.UtcNow;
            var recipientIds = await repository.GetCancellationRecipientsAsync(id, cls.TeacherId);
            repository.AddNotifications(recipientIds.Select(userId => new Notification
            {
                UserId = userId,
                Type = "ClassCancelled",
                Title = "Lớp học đã hủy",
                Message = $"Lớp {cls.Name} đã bị hủy. Các đăng ký đang chờ thanh toán của lớp đã được hủy.",
                ReferenceType = "Class",
                ReferenceId = id,
                CreatedAt = now
            }));
            await repository.CancelPendingEnrollmentsAsync(id, now);
            await repository.CancelUnfinishedSessionsAsync(id, now);
        }

        cls.Status = target;
        cls.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        return new(true, "Đã cập nhật thành công.");
    }
}
