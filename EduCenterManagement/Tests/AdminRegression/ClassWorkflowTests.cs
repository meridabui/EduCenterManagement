using System.Text.Json;
using EduCenterManagement.Controllers.Admin;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.Services.Admin;
using EduCenterManagement.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace AdminRegression;

public class ClassWorkflowTests
{
    [Fact]
    public async Task Create_draft_generates_lessons_and_edit_synchronizes_without_duplicates()
    {
        await using var f = await AdminTestDatabase.CreateAsync();
        var (subject, teacher, slot) = await f.SeedAsync();
        var service = f.Get<IAdminClassService>();
        var form = f.ClassForm(subject, slot, teacher);
        var created = await service.CreateAsync(form);
        Assert.True(created.Success, created.Message);
        var cls = await f.Db.Classes.SingleAsync();
        Assert.Equal(ClassStatus.Draft, cls.Status);
        Assert.Equal(3, await f.Db.ClassSessions.CountAsync());
        var edit = (await service.GetEditAsync(cls.Id)).Data!.Model;
        edit.EndDate = edit.EndDate.AddDays(7);
        var edited = await service.EditAsync(cls.Id, edit, true);
        Assert.True(edited.Success, edited.Message);
        var lessons = await f.Db.ClassSessions.OrderBy(s => s.SessionDate).ToListAsync();
        Assert.Equal(4, lessons.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, lessons.Select(s => s.SessionNumber));
        Assert.All(lessons, s => Assert.Equal(slot.StartTime, s.StartsAt));
        Assert.Single(await f.Db.ClassSchedules.ToListAsync());
        Assert.Equal("Lớp đã có buổi học; không tạo trùng.", (await service.GenerateSessionsAsync(cls.Id)).Message);
        Assert.Equal(4, await f.Db.ClassSessions.CountAsync());
    }

    [Fact]
    public async Task Failed_session_save_rolls_back_class_schedules_and_lessons_together()
    {
        var failure = new FailSaveInterceptor();
        await using var f = await AdminTestDatabase.CreateAsync(failure);
        var (subject, teacher, slot) = await f.SeedAsync();
        failure.FailAfter(3);
        var result = await f.Get<IAdminClassService>().CreateAsync(f.ClassForm(subject, slot, teacher));
        Assert.False(result.Success);
        Assert.Equal("Không thể lưu lớp và lịch học. Vui lòng kiểm tra dữ liệu.", result.Message);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(0, await f.Db.Classes.CountAsync());
        Assert.Equal(0, await f.Db.ClassSchedules.CountAsync());
        Assert.Equal(0, await f.Db.ClassSessions.CountAsync());
        Assert.Equal(1, await f.Db.Subjects.CountAsync());
    }

    [Fact]
    public async Task Early_start_stays_allowed_with_confirmed_student_and_completion_checks_lessons()
    {
        await using var f = await AdminTestDatabase.CreateAsync();
        var (subject, teacher, slot) = await f.SeedAsync();
        var service = f.Get<IAdminClassService>();
        Assert.True((await service.CreateAsync(f.ClassForm(subject, slot, teacher))).Success);
        var cls = await f.Db.Classes.SingleAsync();
        Assert.True((await service.ChangeStatusAsync(cls.Id, ClassStatus.Open)).Success);
        Assert.False((await service.ChangeStatusAsync(cls.Id, ClassStatus.InProgress)).Success);
        await f.EnrollAsync(cls, EnrollmentStatus.Confirm);
        Assert.True(cls.StartDate > DateOnly.FromDateTime(DateTime.Now));
        Assert.True((await service.ChangeStatusAsync(cls.Id, ClassStatus.InProgress)).Success);
        Assert.Equal(ClassStatus.InProgress, cls.Status);
        Assert.False((await service.ChangeStatusAsync(cls.Id, ClassStatus.Completed)).Success);
        foreach (var lesson in await f.Db.ClassSessions.ToListAsync()) lesson.Status = ClassSessionStatus.Completed;
        await f.Db.SaveChangesAsync();
        Assert.True((await service.ChangeStatusAsync(cls.Id, ClassStatus.Completed)).Success);
        Assert.False((await service.ChangeStatusAsync(cls.Id, ClassStatus.Open)).Success);
        Assert.Equal(AdminResultStatus.Invalid, (await service.GetEditAsync(cls.Id)).Status);
    }

    [Fact]
    public async Task Opening_requires_correct_teacher_active_subject_and_nonconflicting_schedule()
    {
        await using var f = await AdminTestDatabase.CreateAsync();
        var (subject, teacher, slot) = await f.SeedAsync();
        var service = f.Get<IAdminClassService>();
        Assert.True((await service.CreateAsync(f.ClassForm(subject, slot))).Success);
        var cls = await f.Db.Classes.SingleAsync();
        Assert.False((await service.ChangeStatusAsync(cls.Id, ClassStatus.Open)).Success);
        Assert.True((await service.AssignTeacherAsync(new() { ClassId = cls.Id, TeacherId = teacher.Id })).Success);
        subject.Status = SubjectStatus.Inactive;
        await f.Db.SaveChangesAsync();
        Assert.Equal("Môn học phải đang hoạt động trước khi mở đăng ký.", (await service.ChangeStatusAsync(cls.Id, ClassStatus.Open)).Message);
        subject.Status = SubjectStatus.Active;
        teacher.User.Status = UserStatus.Inactive;
        await f.Db.SaveChangesAsync();
        Assert.False((await service.ChangeStatusAsync(cls.Id, ClassStatus.Open)).Success);
        teacher.User.Status = UserStatus.Active;
        await f.Db.SaveChangesAsync();
        var conflicting = f.ClassForm(subject, slot, teacher, "Lớp trùng");
        Assert.False((await service.CreateAsync(conflicting)).Success);
        Assert.True((await service.ChangeStatusAsync(cls.Id, ClassStatus.Open)).Success);
    }

    [Fact]
    public async Task Cancellation_updates_pending_enrollments_lessons_and_notifications()
    {
        await using var f = await AdminTestDatabase.CreateAsync();
        var (subject, teacher, slot) = await f.SeedAsync();
        var service = f.Get<IAdminClassService>();
        await service.CreateAsync(f.ClassForm(subject, slot, teacher));
        var cls = await f.Db.Classes.SingleAsync();
        await f.EnrollAsync(cls, EnrollmentStatus.PendingPayment, DateTime.UtcNow.AddMinutes(30));
        Assert.True((await service.ChangeStatusAsync(cls.Id, ClassStatus.Cancelled)).Success);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(EnrollmentStatus.Cancelled, (await f.Db.Enrollments.SingleAsync()).Status);
        Assert.All(await f.Db.ClassSessions.ToListAsync(), s => Assert.Equal(ClassSessionStatus.Cancelled, s.Status));
        Assert.Equal(3, await f.Db.Notifications.CountAsync());
        Assert.Equal(ClassStatus.Cancelled, (await f.Db.Classes.SingleAsync()).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_stays_blocked_by_confirmed_enrollment_or_paid_payment(bool paidPayment)
    {
        await using var f = await AdminTestDatabase.CreateAsync();
        var (subject, teacher, slot) = await f.SeedAsync();
        var service = f.Get<IAdminClassService>();
        await service.CreateAsync(f.ClassForm(subject, slot, teacher));
        var cls = await f.Db.Classes.SingleAsync();
        var enrollment = await f.EnrollAsync(cls, paidPayment ? EnrollmentStatus.PendingPayment : EnrollmentStatus.Confirm);
        if (paidPayment)
        {
            f.Db.Payments.Add(new() { EnrollmentId = enrollment.Id, Status = PaymentStatus.Paid, Amount = 1000000 });
            await f.Db.SaveChangesAsync();
        }
        Assert.False((await service.ChangeStatusAsync(cls.Id, ClassStatus.Cancelled)).Success);
        Assert.Equal(ClassStatus.Draft, cls.Status);
        Assert.Equal(0, await f.Db.Notifications.CountAsync());
        Assert.True((await service.GetDetailsAsync(cls.Id))!.HasPaidEnrollments);
    }

    [Fact]
    public async Task Capacity_excludes_expired_reservations_and_keeps_json_contract()
    {
        await using var f = await AdminTestDatabase.CreateAsync();
        var (subject, teacher, slot) = await f.SeedAsync();
        var service = f.Get<IAdminClassService>();
        await service.CreateAsync(f.ClassForm(subject, slot, teacher));
        var cls = await f.Db.Classes.SingleAsync();
        await f.EnrollAsync(cls, EnrollmentStatus.Confirm);
        await f.EnrollAsync(cls, EnrollmentStatus.PendingPayment, DateTime.UtcNow.AddMinutes(15));
        await f.EnrollAsync(cls, EnrollmentStatus.PendingPayment);
        await f.EnrollAsync(cls, EnrollmentStatus.PendingPayment, DateTime.UtcNow.AddMinutes(-1));
        var controller = new ClassesController(service);
        var result = Assert.IsType<JsonResult>(await controller.Capacity(cls.Id));
        var json = JsonSerializer.SerializeToElement(result.Value);
        Assert.Equal(1, json.GetProperty("confirmed").GetInt32());
        Assert.Equal(2, json.GetProperty("reserved").GetInt32());
        Assert.Equal(3, json.GetProperty("occupied").GetInt32());
        Assert.Equal(17, json.GetProperty("available").GetInt32());
        Assert.Equal(3, json.GetProperty("students").GetArrayLength());
        Assert.True(json.GetProperty("students")[0].TryGetProperty("parentId", out _));
        Assert.IsType<NotFoundResult>(await controller.Capacity(999));
    }

    [Fact]
    public async Task Conflict_endpoints_keep_status_codes_and_payloads()
    {
        await using var f = await AdminTestDatabase.CreateAsync();
        var (subject, teacher, slot) = await f.SeedAsync();
        var service = f.Get<IAdminClassService>();
        var form = f.ClassForm(subject, slot, teacher);
        await service.CreateAsync(form);
        var controller = new ClassesController(service);
        var conflict = Assert.IsType<ConflictObjectResult>(await controller.CheckConflict(teacher.Id, null,
            form.StartDate, form.EndDate, slot.Id, new[] { form.Schedules[0].DayOfWeek }, Array.Empty<int>()));
        var json = JsonSerializer.SerializeToElement(conflict.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.False(json.GetProperty("ok").GetBoolean());
        Assert.Equal(form.Name, json.GetProperty("conflicts")[0].GetProperty("className").GetString());
        var incomplete = Assert.IsType<OkObjectResult>(await controller.CheckConflict(teacher.Id, null,
            form.StartDate, form.EndDate, null, Array.Empty<int>(), Array.Empty<int>()));
        Assert.True(JsonSerializer.SerializeToElement(incomplete.Value).GetProperty("incomplete").GetBoolean());
        Assert.IsType<BadRequestObjectResult>(await controller.CheckConflict(teacher.Id, null, null, null, null, Array.Empty<int>(), Array.Empty<int>()));
        Assert.IsType<BadRequestObjectResult>(await controller.Index(status: "invalid"));
    }

    [Fact]
    public async Task Edit_locks_nondraft_classes_and_rejects_status_changes_through_form()
    {
        await using var f = await AdminTestDatabase.CreateAsync();
        var (subject, teacher, slot) = await f.SeedAsync();
        var service = f.Get<IAdminClassService>();
        await service.CreateAsync(f.ClassForm(subject, slot, teacher));
        var cls = await f.Db.Classes.SingleAsync();
        var model = (await service.GetEditAsync(cls.Id)).Data!.Model;
        model.Status = ClassStatus.Open;
        Assert.Equal(AdminResultStatus.BadRequest, (await service.EditAsync(cls.Id, model, true)).Status);
        model.Status = ClassStatus.Draft;
        await service.ChangeStatusAsync(cls.Id, ClassStatus.Open);
        Assert.Equal(AdminResultStatus.Conflict, (await service.EditAsync(cls.Id, model, true)).Status);
    }

    [Fact]
    public async Task Session_sync_preserves_completed_lesson_while_changing_future_schedule()
    {
        await using var f = await AdminTestDatabase.CreateAsync();
        var (subject, teacher, slot) = await f.SeedAsync();
        var service = f.Get<IAdminClassService>();
        await service.CreateAsync(f.ClassForm(subject, slot, teacher));
        var cls = await f.Db.Classes.SingleAsync();
        var lesson = await f.Db.ClassSessions.OrderBy(s => s.SessionDate).FirstAsync();
        lesson.Status = ClassSessionStatus.Completed;
        var alternate = new TimeSlot { Name = "Ca muộn", StartTime = new(19, 0), EndTime = new(21, 0) };
        f.Db.Add(alternate);
        await f.Db.SaveChangesAsync();
        var model = (await service.GetEditAsync(cls.Id)).Data!.Model;
        model.Schedules[0].TimeSlotId = alternate.Id;
        Assert.True((await service.EditAsync(cls.Id, model, true)).Success);
        f.Db.ChangeTracker.Clear();
        var preserved = await f.Db.ClassSessions.SingleAsync(s => s.Id == lesson.Id);
        Assert.Equal(ClassSessionStatus.Completed, preserved.Status);
        Assert.Equal(new TimeOnly(17, 0), preserved.StartsAt);
        Assert.Null(preserved.ClassScheduleId);
        Assert.All(await f.Db.ClassSessions.Where(s => s.Id != lesson.Id).ToListAsync(), s => Assert.Equal(new TimeOnly(19, 0), s.StartsAt));
    }
}

public sealed class FailSaveInterceptor : SaveChangesInterceptor
{
    private int _remaining = -1;
    public void FailAfter(int saves) => _remaining = saves;
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (_remaining > 0 && --_remaining == 0) throw new DbUpdateException("Injected regression test failure.");
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
