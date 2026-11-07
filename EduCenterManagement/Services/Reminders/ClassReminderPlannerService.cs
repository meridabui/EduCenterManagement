using System.Text.Json;
using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EduCenterManagement.Services.Reminders;

public sealed class ClassReminderPlannerService
{
    public const string TemplateKey = "ClassSessionReminder";

    private readonly ApplicationDbContext _context;
    private readonly TimeProvider _clock;
    private readonly ClassReminderSettings _settings;

    public ClassReminderPlannerService(
        ApplicationDbContext context,
        TimeProvider clock,
        IOptions<ClassReminderSettings> options)
    {
        _context = context;
        _clock = clock;
        _settings = options.Value;
    }

    public async Task<int> PlanAsync(CancellationToken cancellationToken)
    {
        if (!_settings.Enabled)
            return 0;

        var timeZone = ClassReminderTime.ResolveTimeZone(_settings.TimeZoneId);
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var nowLocal = TimeZoneInfo.ConvertTime(_clock.GetUtcNow(), timeZone);
        var today = DateOnly.FromDateTime(nowLocal.DateTime);
        var horizonDays = Math.Clamp(_settings.PlanningHorizonDays, 1, 90);
        var throughDate = today.AddDays(horizonDays);

        var rawCandidates = await (
            from session in _context.ClassSessions.AsNoTracking()
            join enrollment in _context.Enrollments.AsNoTracking()
                on session.ClassId equals enrollment.ClassId
            join student in _context.StudentProfiles.AsNoTracking()
                on enrollment.StudentId equals student.Id
            join user in _context.Users.AsNoTracking()
                on student.UserId equals user.Id
            where session.Status == ClassSessionStatus.Scheduled
                  && session.SessionDate >= today
                  && session.SessionDate <= throughDate
                  && enrollment.Status == EnrollmentStatus.Confirm
                  && user.Status == UserStatus.Active
                  && user.EmailVerifiedAt != null
                  && user.Email != ""
            select new
            {
                SessionId = session.Id,
                session.SessionDate,
                session.StartsAt,
                StudentId = student.Id,
                UserId = user.Id,
                user.Email
            })
            .ToListAsync(cancellationToken);

        var candidates = rawCandidates
            .GroupBy(x => new { x.SessionId, x.StudentId })
            .Select(g => g.First())
            .Where(x => !string.IsNullOrWhiteSpace(x.Email))
            .Where(x => ClassReminderTime.ToUtc(
                x.SessionDate, x.StartsAt, timeZone) > nowUtc)
            .ToList();

        if (candidates.Count == 0)
            return 0;

        var keys = candidates
            .Select(x => BuildDeduplicationKey(x.SessionId, x.StudentId))
            .ToArray();
        var existingByKey = new Dictionary<string, EmailOutbox>(StringComparer.Ordinal);

        foreach (var keyBatch in keys.Chunk(1000))
        {
            var existing = await _context.EmailOutbox
                .Where(x => x.DeduplicationKey != null
                            && keyBatch.Contains(x.DeduplicationKey))
                .ToListAsync(cancellationToken);

            foreach (var item in existing)
                existingByKey[item.DeduplicationKey!] = item;
        }

        var hoursBefore = Math.Clamp(_settings.HoursBefore, 1, 168);
        var created = 0;

        foreach (var candidate in candidates)
        {
            var key = BuildDeduplicationKey(
                candidate.SessionId,
                candidate.StudentId);
            var sessionStartUtc = ClassReminderTime.ToUtc(
                candidate.SessionDate,
                candidate.StartsAt,
                timeZone);
            var dueAtUtc = sessionStartUtc.AddHours(-hoursBefore);

            if (existingByKey.TryGetValue(key, out var existing))
            {
                if (existing.Status == EmailOutboxStatus.Pending)
                {
                    existing.RecipientUserId = candidate.UserId;
                    existing.ToEmail = candidate.Email.Trim();

                    // Preserve a retry delay. Otherwise a planner pass would
                    // continually reset a failed message to "send now".
                    if (existing.RetryCount == 0)
                    {
                        existing.ScheduledAt = dueAtUtc > nowUtc
                            ? dueAtUtc
                            : existing.ScheduledAt > nowUtc
                                ? nowUtc
                                : existing.ScheduledAt;
                    }

                    existing.UpdatedAt = nowUtc;
                }

                continue;
            }

            _context.EmailOutbox.Add(new EmailOutbox
            {
                RecipientUserId = candidate.UserId,
                ToEmail = candidate.Email.Trim(),
                Subject = "[EduCenter] Nhắc lịch học sắp tới",
                TemplateKey = TemplateKey,
                PayloadJson = JsonSerializer.Serialize(
                    new ClassReminderPayload(
                        candidate.SessionId,
                        candidate.StudentId)),
                ScheduledAt = dueAtUtc > nowUtc ? dueAtUtc : nowUtc,
                Status = EmailOutboxStatus.Pending,
                DeduplicationKey = key,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            });

            created++;
        }

        if (_context.ChangeTracker.HasChanges())
            await _context.SaveChangesAsync(cancellationToken);

        return created;
    }

    private static string BuildDeduplicationKey(int sessionId, int studentId) =>
        $"class-reminder:{sessionId}:{studentId}";
}
