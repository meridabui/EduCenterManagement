using System.Text.Json;
using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Services.Email;

public sealed record EnrollmentConfirmationPayload(int EnrollmentId);
public sealed record EnrollmentConfirmationEmailRuntime(DateTime StartedAtUtc);

public sealed class EnrollmentConfirmationPlannerService
{
    public const string TemplateKey = "EnrollmentConfirmed";
    private const string DeduplicationPrefix = "enrollment-confirmed:";

    private readonly ApplicationDbContext _context;
    private readonly TimeProvider _clock;
    private readonly EnrollmentConfirmationEmailRuntime _runtime;

    public EnrollmentConfirmationPlannerService(
        ApplicationDbContext context,
        TimeProvider clock,
        EnrollmentConfirmationEmailRuntime runtime)
    {
        _context = context;
        _clock = clock;
        _runtime = runtime;
    }

    public async Task<int> PlanAsync(CancellationToken cancellationToken)
    {
        var candidates = await _context.Enrollments
            .AsNoTracking()
            .Where(enrollment =>
                enrollment.Status == EnrollmentStatus.Confirm &&
                enrollment.ConfirmedAt != null &&
                enrollment.ConfirmedAt >= _runtime.StartedAtUtc &&
                enrollment.RegisteredByParent.User.Status == UserStatus.Active &&
                enrollment.RegisteredByParent.User.EmailVerifiedAt != null &&
                enrollment.RegisteredByParent.User.Email != "")
            .Select(enrollment => new
            {
                EnrollmentId = enrollment.Id,
                RecipientUserId = enrollment.RegisteredByParent.UserId,
                Email = enrollment.RegisteredByParent.User.Email,
                ClassName = enrollment.Class.Name
            })
            .ToListAsync(cancellationToken);

        candidates = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Email))
            .ToList();

        if (candidates.Count == 0)
            return 0;

        var keys = candidates
            .Select(candidate => BuildDeduplicationKey(candidate.EnrollmentId))
            .ToArray();
        var existingKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var keyBatch in keys.Chunk(1000))
        {
            var existing = await _context.EmailOutbox
                .AsNoTracking()
                .Where(message => message.DeduplicationKey != null &&
                                  keyBatch.Contains(message.DeduplicationKey))
                .Select(message => message.DeduplicationKey!)
                .ToListAsync(cancellationToken);
            existingKeys.UnionWith(existing);
        }

        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var created = 0;

        foreach (var candidate in candidates)
        {
            var key = BuildDeduplicationKey(candidate.EnrollmentId);
            if (existingKeys.Contains(key))
                continue;

            _context.EmailOutbox.Add(new Models.EmailOutbox
            {
                RecipientUserId = candidate.RecipientUserId,
                ToEmail = candidate.Email.Trim(),
                Subject = $"[EduCenter] Đăng ký lớp học thành công - {candidate.ClassName}",
                TemplateKey = TemplateKey,
                PayloadJson = JsonSerializer.Serialize(
                    new EnrollmentConfirmationPayload(candidate.EnrollmentId)),
                ScheduledAt = nowUtc,
                Status = EmailOutboxStatus.Pending,
                DeduplicationKey = key,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            });
            existingKeys.Add(key);
            created++;
        }

        if (created > 0)
            await _context.SaveChangesAsync(cancellationToken);

        return created;
    }

    private static string BuildDeduplicationKey(int enrollmentId) =>
        $"{DeduplicationPrefix}{enrollmentId}";
}
