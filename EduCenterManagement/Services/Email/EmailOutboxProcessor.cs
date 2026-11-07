using System.Text.Json;
using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Services.Reminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EduCenterManagement.Services.Email;

public sealed class EmailOutboxProcessor
{
    private readonly ApplicationDbContext _context;
    private readonly IAppEmailSender _emailSender;
    private readonly TimeProvider _clock;
    private readonly ClassReminderSettings _settings;
    private readonly ILogger<EmailOutboxProcessor> _logger;

    public EmailOutboxProcessor(
        ApplicationDbContext context,
        IAppEmailSender emailSender,
        TimeProvider clock,
        IOptions<ClassReminderSettings> options,
        ILogger<EmailOutboxProcessor> logger)
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
            .Where(x => x.TemplateKey == ClassReminderPlannerService.TemplateKey
                        && x.Status == EmailOutboxStatus.Pending
                        && x.ScheduledAt <= nowUtc)
            .OrderBy(x => x.ScheduledAt)
            .ThenBy(x => x.Id)
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
        {
            await ProcessOneAsync(message, cancellationToken);
        }

        return messages.Count;
    }

    private async Task ProcessOneAsync(
        Models.EmailOutbox message,
        CancellationToken cancellationToken)
    {
        ClassReminderPayload? payload;

        try
        {
            payload = JsonSerializer.Deserialize<ClassReminderPayload>(
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
                "Payload nhắc lịch bị thiếu.",
                cancellationToken);
            return;
        }

        var session = await _context.ClassSessions
            .AsNoTracking()
            .Include(x => x.Class)
                .ThenInclude(x => x.Subject)
            .Include(x => x.Class)
                .ThenInclude(x => x.Teacher)
            .SingleOrDefaultAsync(
                x => x.Id == payload.SessionId,
                cancellationToken);

        var student = await _context.StudentProfiles
            .AsNoTracking()
            .Include(x => x.User)
            .SingleOrDefaultAsync(
                x => x.Id == payload.StudentId,
                cancellationToken);

        var enrollmentIsConfirmed = session != null && await _context.Enrollments
            .AsNoTracking()
            .AnyAsync(
                x => x.StudentId == payload.StudentId
                     && x.ClassId == session.ClassId
                     && x.Status == EnrollmentStatus.Confirm,
                cancellationToken);

        if (session == null
            || session.Status != ClassSessionStatus.Scheduled
            || student == null
            || student.User.Status != UserStatus.Active
            || student.User.EmailVerifiedAt == null
            || string.IsNullOrWhiteSpace(student.User.Email)
            || !enrollmentIsConfirmed)
        {
            await CancelAsync(
                message,
                "Buổi học, đăng ký hoặc tài khoản học sinh không còn hợp lệ.",
                cancellationToken);
            return;
        }

        var timeZone = ClassReminderTime.ResolveTimeZone(_settings.TimeZoneId);
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var startsAtUtc = ClassReminderTime.ToUtc(
            session.SessionDate,
            session.StartsAt,
            timeZone);

        if (startsAtUtc <= nowUtc)
        {
            await CancelAsync(
                message,
                "Buổi học đã bắt đầu hoặc đã kết thúc.",
                cancellationToken);
            return;
        }

        var hoursBefore = Math.Clamp(_settings.HoursBefore, 1, 168);
        var dueAtUtc = startsAtUtc.AddHours(-hoursBefore);

        // A schedule may have been moved after the outbox row was created.
        if (dueAtUtc > nowUtc)
        {
            message.Status = EmailOutboxStatus.Pending;
            message.ScheduledAt = dueAtUtc;
            message.RetryCount = 0;
            message.LastError = null;
            message.UpdatedAt = nowUtc;
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        var email = ClassReminderEmailTemplate.Build(new ClassReminderEmailData(
            student.FullName,
            session.Class.Name,
            session.Class.Subject.Name,
            session.Class.Teacher?.FullName,
            session.SessionNumber,
            session.SessionDate,
            session.StartsAt,
            session.EndsAt));

        try
        {
            await _emailSender.SendAsync(
                student.User.Email.Trim(),
                email.Subject,
                email.HtmlBody,
                cancellationToken);

            message.ToEmail = student.User.Email.Trim();
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
            .Where(x => x.TemplateKey == ClassReminderPlannerService.TemplateKey
                        && x.Status == EmailOutboxStatus.Processing
                        && x.UpdatedAt < staleBefore)
            .ToListAsync(cancellationToken);

        if (interrupted.Count == 0)
            return;

        foreach (var message in interrupted)
        {
            message.Status = EmailOutboxStatus.Pending;
            message.ScheduledAt = nowUtc;
            message.LastError = "Lần xử lý trước bị gián đoạn; hệ thống sẽ thử lại.";
            message.UpdatedAt = nowUtc;
        }

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
                "Class reminder email {OutboxId} failed permanently after {RetryCount} attempt(s).",
                message.Id,
                message.RetryCount);
        }
        else
        {
            message.Status = EmailOutboxStatus.Pending;
            message.ScheduledAt = nowUtc.Add(GetRetryDelay(message.RetryCount));
            _logger.LogWarning(
                exception,
                "Class reminder email {OutboxId} failed. Retry {RetryCount}/{MaxRetryCount} scheduled.",
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
