using System.Net;

namespace EduCenterManagement.Services.Email;

public sealed record EnrollmentConfirmationEmailData(
    string ParentName,
    string StudentName,
    int EnrollmentId,
    string ClassName,
    string SubjectName,
    decimal Fee,
    string ConfirmedAt,
    IReadOnlyCollection<string> Schedules);

public static class EnrollmentConfirmationEmailTemplate
{
    public static (string Subject, string HtmlBody) Build(
        EnrollmentConfirmationEmailData data)
    {
        var parentName = Encode(data.ParentName);
        var studentName = Encode(data.StudentName);
        var className = Encode(data.ClassName);
        var subjectName = Encode(data.SubjectName);
        var confirmedAt = Encode(data.ConfirmedAt);
        var scheduleHtml = data.Schedules.Count == 0
            ? "<li>Trung tâm sẽ cập nhật lịch học sau.</li>"
            : string.Join(string.Empty, data.Schedules.Select(schedule =>
                $"<li style=\"margin-bottom:6px\">{Encode(schedule)}</li>"));

        var safeClassName = data.ClassName
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        var subject = $"[EduCenter] Đăng ký lớp học thành công - {safeClassName}";
        var body = $$"""
            <!doctype html>
            <html lang="vi">
            <body style="margin:0;padding:24px;background:#f4f6f8;font-family:Arial,sans-serif;color:#1f2937">
              <div style="max-width:620px;margin:0 auto;background:#ffffff;border-radius:12px;padding:28px;border:1px solid #e5e7eb">
                <h2 style="margin:0 0 16px;color:#15803d">Đăng ký lớp học thành công</h2>
                <p>Chào <strong>{{parentName}}</strong>,</p>
                <p>EduCenter xác nhận học phí đã được thanh toán và đăng ký lớp học đã hoàn tất.</p>
                <table style="width:100%;border-collapse:collapse;margin:20px 0">
                  <tr><td style="padding:8px 0;color:#6b7280">Mã đăng ký</td><td style="padding:8px 0"><strong>#{{data.EnrollmentId}}</strong></td></tr>
                  <tr><td style="padding:8px 0;color:#6b7280">Học sinh</td><td style="padding:8px 0">{{studentName}}</td></tr>
                  <tr><td style="padding:8px 0;color:#6b7280">Môn học</td><td style="padding:8px 0">{{subjectName}}</td></tr>
                  <tr><td style="padding:8px 0;color:#6b7280">Lớp</td><td style="padding:8px 0">{{className}}</td></tr>
                  <tr><td style="padding:8px 0;color:#6b7280">Học phí</td><td style="padding:8px 0"><strong>{{data.Fee:N0}} đ</strong></td></tr>
                  <tr><td style="padding:8px 0;color:#6b7280">Xác nhận lúc</td><td style="padding:8px 0">{{confirmedAt}}</td></tr>
                </table>
                <h3 style="font-size:16px;margin:20px 0 8px">Lịch học hàng tuần</h3>
                <ul style="padding-left:20px;margin-top:8px">{{scheduleHtml}}</ul>
                <p style="margin-top:28px;color:#6b7280;font-size:13px">Đây là email tự động từ EduCenter.</p>
              </div>
            </body>
            </html>
            """;

        return (subject, body);
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
