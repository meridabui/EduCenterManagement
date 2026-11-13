using EduCenterManagement.Repositories.Admin;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;

namespace EduCenterManagement.Services.Admin;

public sealed record ClassSessionSyncResult(int Added, int Removed, int Total);

// Called only from Admin write actions, within the same transaction as the class/schedules.
public class ClassSessionService(IAdminClassRepository repository, IAdminUnitOfWork unitOfWork) : IClassSessionService
{
    public async Task<ClassSessionSyncResult> SynchronizeAsync(
        Class classItem, IReadOnlyCollection<ClassSchedule> schedules, bool onlyIfEmpty = false)
    {
        var existing = await repository.GetSessionsAsync(classItem.Id);

        if (onlyIfEmpty && existing.Count > 0)
            return new(0, 0, existing.Count);

        var referencedIds = await repository.GetReferencedSessionIdsAsync(classItem.Id);

        var now = DateTime.Now;
        var utcNow = DateTime.UtcNow;
        var referenced = referencedIds.ToHashSet();
        bool IsProtected(ClassSession s) => s.SessionDate.ToDateTime(s.StartsAt) <= now
            || s.Status != ClassSessionStatus.Scheduled || referenced.Contains(s.Id);

        var protectedSessions = existing.Where(IsProtected).ToList();
        var protectedDates = protectedSessions.Select(s => s.SessionDate).ToHashSet();
        var planned = new List<ClassSession>();
        if (classItem.StartDate <= classItem.EndDate)
        {
            // DayNumber avoids overflowing DateOnly.MaxValue at the end of the range.
            for (int day = classItem.StartDate.DayNumber; day <= classItem.EndDate.DayNumber; day++)
            {
                var date = DateOnly.FromDayNumber(day);
                int weekday = date.DayOfWeek == DayOfWeek.Sunday ? 8 : (int)date.DayOfWeek + 1;
                foreach (var schedule in schedules.Where(s => s.DayOfWeek == weekday)
                             .OrderBy(s => s.TimeSlot.StartTime))
                {
                    // Backfill an empty class fully; edits never invent new past lessons.
                    if (existing.Count > 0 && date.ToDateTime(schedule.TimeSlot.StartTime) <= now)
                        continue;
                    // One lesson per weekday: preserve an occupied lesson even if its time changed.
                    if (protectedDates.Contains(date))
                        continue;
                    planned.Add(new ClassSession
                    {
                        ClassId = classItem.Id,
                        ClassScheduleId = schedule.Id,
                        SessionDate = date,
                        StartsAt = schedule.TimeSlot.StartTime,
                        EndsAt = schedule.TimeSlot.EndTime,
                        Status = ClassSessionStatus.Scheduled,
                        CreatedAt = utcNow,
                        UpdatedAt = utcNow
                    });
                }
            }
        }

        static (DateOnly, TimeOnly, TimeOnly) Key(ClassSession s) => (s.SessionDate, s.StartsAt, s.EndsAt);
        var desired = planned.ToDictionary(Key);
        var remaining = new List<ClassSession>(protectedSessions);
        int removed = 0;
        foreach (var session in existing)
        {
            if (IsProtected(session))
            {
                // Detach historical lessons from removed schedules, without changing lesson data.
                var matching = schedules.FirstOrDefault(s =>
                    s.DayOfWeek == (session.SessionDate.DayOfWeek == DayOfWeek.Sunday
                        ? 8 : (int)session.SessionDate.DayOfWeek + 1)
                    && s.TimeSlot.StartTime == session.StartsAt && s.TimeSlot.EndTime == session.EndsAt);
                if (session.ClassScheduleId != matching?.Id)
                {
                    session.ClassScheduleId = matching?.Id;
                    session.ClassSchedule = matching;
                }
                continue;
            }

            if (desired.Remove(Key(session), out var match))
            {
                session.ClassScheduleId = match.ClassScheduleId;
                remaining.Add(session);
            }
            else
            {
                repository.RemoveSession(session);
                removed++;
            }
        }

        var added = desired.Values.ToList();
        remaining.AddRange(added);
        var reservedNumbers = protectedSessions.Select(s => s.SessionNumber).ToHashSet();
        int number = 1;
        foreach (var session in remaining.Except(protectedSessions)
                     .OrderBy(s => s.SessionDate).ThenBy(s => s.StartsAt))
        {
            while (reservedNumbers.Contains(number)) number++;
            if (session.SessionNumber != number)
            {
                session.SessionNumber = number;
                session.UpdatedAt = utcNow;
            }
            number++;
        }

        repository.AddSessions(added);
        await unitOfWork.SaveChangesAsync();
        return new(added.Count, removed, remaining.Count);
    }
}
