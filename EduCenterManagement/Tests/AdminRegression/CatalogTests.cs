using EduCenterManagement.Controllers.Admin;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.Services.Admin;
using EduCenterManagement.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AdminRegression;

public class CatalogTests
{
    [Fact]
    public async Task Subjects_keep_trimming_duplicate_errors_and_soft_delete()
    {
        await using var f = await AdminTestDatabase.CreateAsync();
        var service = f.Get<IAdminSubjectService>();
        Assert.True((await service.CreateAsync(new() { Name = "  Toán  ", Description = "  Ôn thi  " })).Success);
        var subject = await f.Db.Subjects.SingleAsync();
        Assert.Equal("Toán", subject.Name);
        Assert.Equal("Ôn thi", subject.Description);
        var duplicate = await service.CreateAsync(new() { Name = "Toán" });
        Assert.Equal(new AdminValidationError("Name", "Tên môn học đã tồn tại."), Assert.Single(duplicate.Errors));
        Assert.True((await service.DeactivateAsync(subject.Id)).Success);
        Assert.Equal(SubjectStatus.Inactive, subject.Status);
        Assert.Equal("Môn học này đã ngừng hoạt động.", (await service.DeactivateAsync(subject.Id)).Message);
        Assert.Equal(AdminResultStatus.NotFound, (await service.EditAsync(999, new() { Name = "Khác" })).Status);
    }

    [Theory]
    [InlineData(ClassStatus.Draft)]
    [InlineData(ClassStatus.Open)]
    [InlineData(ClassStatus.InProgress)]
    [InlineData(ClassStatus.Completed)]
    [InlineData(ClassStatus.Cancelled)]
    public async Task Used_slot_locks_hours_for_every_class_status_but_allows_rename(ClassStatus status)
    {
        await using var f = await AdminTestDatabase.CreateAsync();
        var (subject, _, slot) = await f.SeedAsync();
        f.Db.Classes.Add(new Class
        {
            Name = "Lớp cũ", SubjectId = subject.Id, Status = status, MaxStudents = 20,
            StartDate = new(2027, 1, 1), EndDate = new(2027, 2, 1),
            ClassSchedules = new List<ClassSchedule> { new() { DayOfWeek = 2, TimeSlotId = slot.Id } }
        });
        await f.Db.SaveChangesAsync();
        var service = f.Get<IAdminTimeSlotService>();
        var model = (await service.GetEditAsync(slot.Id))!;
        Assert.True(model.IsTimeLocked);
        model.StartTime = new(18, 0);
        model.Name = "Tên mới";
        Assert.False((await service.EditAsync(slot.Id, model, true, true)).Success);
        Assert.Equal(new TimeOnly(17, 0), model.StartTime);
        Assert.Equal("Ca tối", slot.Name);
        Assert.True((await service.EditAsync(slot.Id, model, true, true)).Success);
        Assert.Equal("Tên mới", slot.Name);
        Assert.Equal(new TimeOnly(17, 0), slot.StartTime);
    }

    [Fact]
    public async Task Slot_is_rechecked_after_a_class_starts_using_it_while_form_is_open()
    {
        await using var f = await AdminTestDatabase.CreateAsync();
        var (subject, _, slot) = await f.SeedAsync();
        var service = f.Get<IAdminTimeSlotService>();
        var model = (await service.GetEditAsync(slot.Id))!;
        Assert.False(model.IsTimeLocked);
        f.Db.Classes.Add(new Class
        {
            Name = "Vừa tạo", SubjectId = subject.Id, MaxStudents = 20,
            StartDate = new(2027, 1, 1), EndDate = new(2027, 2, 1),
            ClassSchedules = new List<ClassSchedule> { new() { DayOfWeek = 2, TimeSlotId = slot.Id } }
        });
        await f.Db.SaveChangesAsync();
        model.StartTime = new(18, 0);
        var result = await service.EditAsync(slot.Id, model, true, true);
        Assert.False(result.Success);
        Assert.True(model.IsTimeLocked);
        Assert.Equal(new TimeOnly(17, 0), (await f.Db.TimeSlots.AsNoTracking().SingleAsync()).StartTime);
    }

    [Fact]
    public async Task Unused_slot_can_change_hours_and_inactive_overlaps_still_block_creation()
    {
        await using var f = await AdminTestDatabase.CreateAsync();
        var (_, _, slot) = await f.SeedAsync();
        var service = f.Get<IAdminTimeSlotService>();
        var model = (await service.GetEditAsync(slot.Id))!;
        model.StartTime = new(18, 0);
        model.EndTime = new(20, 0);
        Assert.True((await service.EditAsync(slot.Id, model, true, true)).Success);
        Assert.True((await service.DeactivateAsync(slot.Id)).Success);
        var result = await service.CreateAsync(new() { Name = "Ca khác", StartTime = new(19, 0), EndTime = new(21, 0) }, true);
        Assert.Equal("Khung giờ mới bị trùng hoặc chồng lấn với khung giờ hiện có.", result.Message);
    }

    [Fact]
    public async Task Locked_times_restore_modelstate_without_clearing_name_errors()
    {
        await using var f = await AdminTestDatabase.CreateAsync();
        var (subject, _, slot) = await f.SeedAsync();
        f.Db.Classes.Add(new Class
        {
            Name = "Lớp", SubjectId = subject.Id, MaxStudents = 20,
            StartDate = new(2027, 1, 1), EndDate = new(2027, 2, 1),
            ClassSchedules = new List<ClassSchedule> { new() { DayOfWeek = 2, TimeSlotId = slot.Id } }
        });
        await f.Db.SaveChangesAsync();
        var controller = new TimeSlotsController(f.Get<IAdminTimeSlotService>());
        controller.ModelState.AddModelError("Name", "Vui lòng nhập tên khung giờ.");
        controller.ModelState.AddModelError("StartTime", "Invalid time");
        var model = new TimeSlotViewModel { Id = slot.Id, Name = "", StartTime = default, EndTime = slot.EndTime };
        var view = Assert.IsType<ViewResult>(await controller.Edit(slot.Id, model));
        Assert.Equal("~/Views/Admin/TimeSlots/Edit.cshtml", view.ViewName);
        Assert.False(controller.ModelState.ContainsKey("StartTime"));
        Assert.True(controller.ModelState.ContainsKey("Name"));
        Assert.Equal(new TimeOnly(17, 0), model.StartTime);
        Assert.NotEmpty(controller.ModelState[string.Empty]!.Errors);
    }

    [Fact]
    public async Task Default_slots_skip_existing_entries_and_keep_six_total()
    {
        await using var f = await AdminTestDatabase.CreateAsync();
        var service = f.Get<IAdminTimeSlotService>();
        Assert.True((await service.GenerateDefaultSlotsAsync()).Success);
        Assert.Equal(6, await f.Db.TimeSlots.CountAsync());
        Assert.False((await service.GenerateDefaultSlotsAsync()).Success);
        Assert.Equal(6, await f.Db.TimeSlots.CountAsync());
    }
}
