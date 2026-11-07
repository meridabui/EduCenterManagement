using System.Net;

namespace EduCenterManagement.Services.Reminders;

public sealed record ClassReminderEmailData(
    string StudentName,
    string ClassName,
    string SubjectName,
    string? TeacherName,
    int SessionNumber,
    DateOnly SessionDate,
    TimeOnly StartsAt,
    TimeOnly EndsAt);

public static class ClassReminderEmailTemplate
{
    public static (string Subject, string HtmlBody) Build(
        ClassReminderEmailData data)
    {
        var studentName = Encode(data.StudentName);
        var className = Encode(data.ClassName);
        var subjectName = Encode(data.SubjectName);
        var teacherName = string.IsNullOrWhiteSpace(data.TeacherName)
            ? "Chưa phân công"
            : Encode(data.TeacherName);
        var weekday = GetVietnameseWeekday(data.SessionDate.DayOfWeek);
        var date = data.SessionDate.ToString("dd/MM/yyyy");
        var start = data.StartsAt.ToString("HH:mm");
        var end = data.EndsAt.ToString("HH:mm");

        var safeSubjectName = data.SubjectName
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        var subject = $"[EduCenter] Nhắc lịch học {safeSubjectName} - {start} ngày {date}";
        var body = $$"""
            <!doctype html>
            <html lang="vi">
            <body style="margin:0;padding:24px;background:#f4f6f8;font-family:Arial,sans-serif;color:#1f2937">
              <div style="max-width:620px;margin:0 auto;background:#ffffff;border-radius:12px;padding:28px;border:1px solid #e5e7eb">
                <h2 style="margin:0 0 16px;color:#1d4ed8">Nhắc lịch học sắp tới</h2>
                <p>Chào <strong>{{studentName}}</strong>,</p>
                <p>Bạn có một buổi học sẽ bắt đầu sau khoảng 24 giờ:</p>
                <table style="width:100%;border-collapse:collapse;margin:20px 0">
                  <tr><td style="padding:8px 0;color:#6b7280">Môn học</td><td style="padding:8px 0"><strong>{{subjectName}}</strong></td></tr>
                  <tr><td style="padding:8px 0;color:#6b7280">Lớp</td><td style="padding:8px 0">{{className}}</td></tr>
                  <tr><td style="padding:8px 0;color:#6b7280">Buổi</td><td style="padding:8px 0">{{data.SessionNumber}}</td></tr>
                  <tr><td style="padding:8px 0;color:#6b7280">Thời gian</td><td style="padding:8px 0"><strong>{{start}}–{{end}}, {{weekday}} ngày {{date}}</strong></td></tr>
                  <tr><td style="padding:8px 0;color:#6b7280">Giáo viên</td><td style="padding:8px 0">{{teacherName}}</td></tr>
                </table>
                <p>Vui lòng chuẩn bị đầy đủ tài liệu và có mặt đúng giờ.</p>
                <p style="margin-top:28px;color:#6b7280;font-size:13px">Đây là email tự động từ EduCenter.</p>
              </div>
            </body>
            </html>
            """;

        return (subject, body);
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static string GetVietnameseWeekday(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Thứ Hai",
        DayOfWeek.Tuesday => "Thứ Ba",
        DayOfWeek.Wednesday => "Thứ Tư",
        DayOfWeek.Thursday => "Thứ Năm",
        DayOfWeek.Friday => "Thứ Sáu",
        DayOfWeek.Saturday => "Thứ Bảy",
        DayOfWeek.Sunday => "Chủ Nhật",
        _ => string.Empty
    };
}
