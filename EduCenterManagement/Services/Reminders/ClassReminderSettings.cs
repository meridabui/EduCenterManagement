namespace EduCenterManagement.Services.Reminders;

public sealed class ClassReminderSettings
{
    public const string SectionName = "ClassReminder";

    // Mỗi máy tự bật sau khi đã cấu hình SMTP/User Secrets.
    public bool Enabled { get; set; } = false;
    public int HoursBefore { get; set; } = 24;
    public int PlanningHorizonDays { get; set; } = 7;
    public int PlannerIntervalMinutes { get; set; } = 5;
    public int SenderIntervalSeconds { get; set; } = 60;
    public int MaxRetryCount { get; set; } = 5;
    public int BatchSize { get; set; } = 20;
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
}

internal static class ClassReminderTime
{
    public static TimeZoneInfo ResolveTimeZone(string configuredId)
    {
        var id = string.IsNullOrWhiteSpace(configuredId)
            ? "Asia/Ho_Chi_Minh"
            : configuredId.Trim();

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (
            id == "Asia/Ho_Chi_Minh"
            && ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }

    public static DateTime ToUtc(
        DateOnly date,
        TimeOnly time,
        TimeZoneInfo timeZone)
    {
        var local = DateTime.SpecifyKind(
            date.ToDateTime(time),
            DateTimeKind.Unspecified);

        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }
}
