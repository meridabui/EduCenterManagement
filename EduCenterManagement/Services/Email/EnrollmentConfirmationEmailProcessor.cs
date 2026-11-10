using System.Text.Json;
using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Services.Reminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EduCenterManagement.Services.Email;

public sealed class EnrollmentConfirmationEmailProcessor
{
    private readonly ApplicationDbContext _context;
    private readonly IAppEmailSender _emailSender;
    private readonly TimeProvider _clock;
    private readonly ClassReminderSettings _settings;
    private readonly ILogger<EnrollmentConfirmationEmailProcessor> _logger;

    public EnrollmentConfirmationEmailProcessor(
        ApplicationDbContext context,
        IAppEmailSender emailSender,
        TimeProvider clock,
        IOptions<ClassReminderSettings> options,
        ILogger<EnrollmentConfirmationEmailProcessor> logger)
    {
        _context = context;
        _emailSender = emailSender;
        _clock = clock;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken)
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        await RecoverInterruptedMessagesAsync(nowUtc, cancellationToken);

        var batchSize = Math.Clamp(_settings.BatchSize, 1, 100);
        var messages = await _context.EmailOutbox
            .Where(message =>
                message.TemplateKey == EnrollmentConfirmationPlannerService.TemplateKey &&
                message.Status == EmailOutboxStatus.Pending &&
                message.ScheduledAt <= nowUtc)
            .OrderBy(message => message.ScheduledAt)
            .ThenBy(message => message.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
            return 0;

        foreach (var message in messages)
        {
            message.Status = EmailOutboxStatus.Processing;
            message.UpdatedAt = nowUtc;
        }
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var message in messages)
            await ProcessOneAsync(message, cancellationToken);

        return messages.Count;
    }

    private async Task ProcessOneAsync(
        Models.EmailOutbox message,
        CancellationToken cancellationToken)
    {
        EnrollmentConfirmationPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<EnrollmentConfirmationPayload>(
                message.PayloadJson ?? string.Empty);
        }
        catch (JsonException ex)
        {
            await MarkPermanentFailureAsync(
                message,
                $"Payload không hợp lệ: {ex.Message}",
                cancellationToken);
            return;
        }

        if (payload == null)
        {
            await MarkPermanentFailureAsync(
                message,
                "Payload xác nhận đăng ký bị thiếu.",
                cancellationToken);
            return;
        }

        var enrollment = await _context.Enrollments
            .AsNoTracking()
            .Include(item => item.Student)
            .Include(item => item.RegisteredByParent)
                .ThenInclude(parent => parent.User)
            .Include(item => item.Class)
                .ThenInclude(classItem => classItem.Subject)
            .Include(item => item.Class)
                .ThenInclude(classItem => classItem.ClassSchedules)
                    .ThenInclude(schedule => schedule.TimeSlot)
            .SingleOrDefaultAsync(
                item => item.Id == payload.EnrollmentId,
                cancellationToken);

        if (enrollment == null ||
            enrollment.Status != EnrollmentStatus.Confirm ||
            enrollment.RegisteredByParent.User.Status != UserStatus.Active ||
            enrollment.RegisteredByParent.User.EmailVerifiedAt == null ||
            string.IsNullOrWhiteSpace(enrollment.RegisteredByParent.User.Email))
        {
            await CancelAsync(
                message,
                "Đăng ký hoặc tài khoản phụ huynh không còn hợp lệ.",
                cancellationToken);
            return;
        }

        var timeZone = ClassReminderTime.ResolveTimeZone(_settings.TimeZoneId);
        var confirmedUtc = DateTime.SpecifyKind(
            enrollment.ConfirmedAt ?? enrollment.UpdatedAt,
            DateTimeKind.Utc);
        var confirmedLocal = TimeZoneInfo.ConvertTimeFromUtc(confirmedUtc, timeZone);
        var schedules = enrollment.Class.ClassSchedules
            .OrderBy(schedule => schedule.DayOfWeek)
            .ThenBy(schedule => schedule.TimeSlot.StartTime)
            .Select(schedule =>
                $"{DayLabel(schedule.DayOfWeek)}, " +
                $"{schedule.TimeSlot.StartTime:HH:mm}-{schedule.TimeSlot.EndTime:HH:mm} " +
                $"({schedule.TimeSlot.Name})")
            .ToList();
        var email = EnrollmentConfirmationEmailTemplate.Build(
            new EnrollmentConfirmationEmailData(
                enrollment.RegisteredByParent.FullName,
                enrollment.Student.FullName,
                enrollment.Id,
                enrollment.Class.Name,
                enrollment.Class.Subject.Name,
                enrollment.Class.Fee,
                confirmedLocal.ToString("HH:mm dd/MM/yyyy"),
                schedules));

        try
        {
            var toEmail = enrollment.RegisteredByParent.User.Email.Trim();
            await _emailSender.SendAsync(
                toEmail,
                email.Subject,
                email.HtmlBody,
                cancellationToken);

            message.ToEmail = toEmail;
            message.Subject = email.Subject;
            message.Status = EmailOutboxStatus.Sent;
            message.SentAt = _clock.GetUtcNow().UtcDateTime;
            message.LastError = null;
            message.UpdatedAt = message.SentAt.Value;
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await ScheduleRetryOrFailAsync(message, ex, cancellationToken);
        }
    }

    private async Task RecoverInterruptedMessagesAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var staleBefore = nowUtc.AddMinutes(-10);
        var interrupted = await _context.EmailOutbox
            .Where(message =>
                message.TemplateKey == EnrollmentConfirmationPlannerService.TemplateKey &&
                message.Status == EmailOutboxStatus.Processing &&
                message.UpdatedAt < staleBefore)
            .ToListAsync(cancellationToken);

        foreach (var message in interrupted)
        {
            message.Status = EmailOutboxStatus.Pending;
            message.ScheduledAt = nowUtc;
            message.LastError = "Lần xử lý trước bị gián đoạn; hệ thống sẽ thử lại.";
            message.UpdatedAt = nowUtc;
        }

        if (interrupted.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task ScheduleRetryOrFailAsync(
        Models.EmailOutbox message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var maxRetries = Math.Clamp(_settings.MaxRetryCount, 1, 20);
        message.RetryCount++;
        message.LastError = Truncate(exception.GetBaseException().Message, 1000);
        message.UpdatedAt = nowUtc;

        if (message.RetryCount >= maxRetries)
        {
            message.Status = EmailOutboxStatus.Failed;
            _logger.LogError(
                exception,
                "Enrollment confirmation email {OutboxId} failed permanently after {RetryCount} attempt(s).",
                message.Id,
                message.RetryCount);
        }
        else
        {
            message.Status = EmailOutboxStatus.Pending;
            message.ScheduledAt = nowUtc.Add(GetRetryDelay(message.RetryCount));
            _logger.LogWarning(
                exception,
                "Enrollment confirmation email {OutboxId} failed. Retry {RetryCount}/{MaxRetryCount} scheduled.",
                message.Id,
                message.RetryCount,
                maxRetries);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkPermanentFailureAsync(
        Models.EmailOutbox message,
        string error,
        CancellationToken cancellationToken)
    {
        message.Status = EmailOutboxStatus.Failed;
        message.LastError = Truncate(error, 1000);
        message.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task CancelAsync(
        Models.EmailOutbox message,
        string reason,
        CancellationToken cancellationToken)
    {
        message.Status = EmailOutboxStatus.Cancelled;
        message.LastError = Truncate(reason, 1000);
        message.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string DayLabel(int dayOfWeek) =>
        dayOfWeek == 8 ? "Chủ nhật" : $"Thứ {dayOfWeek}";

    private static TimeSpan GetRetryDelay(int retryCount) => retryCount switch
    {
        1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(5),
        3 => TimeSpan.FromMinutes(15),
        4 => TimeSpan.FromMinutes(30),
        _ => TimeSpan.FromHours(1)
    };

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
