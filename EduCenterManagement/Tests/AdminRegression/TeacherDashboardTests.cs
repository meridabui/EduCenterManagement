using EduCenterManagement.Controllers.Admin;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.Services.Admin;
using EduCenterManagement.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AdminRegression;

public class TeacherDashboardTests
{
    [Fact]
    public async Task Teacher_creation_keeps_email_normalization_password_hash_and_subjects()
    {
        await using var f = await AdminTestDatabase.CreateAsync();
        var (subject, _, _) = await f.SeedAsync();
        var model = new CreateTeacherViewModel
        {
            Email = "  NEW@TEST.LOCAL  ", FullName = "  Trần Bình  ", Password = "StrongPassword1!",
            SubjectIds = new() { subject.Id, subject.Id }
        };
        // Successful posts write TempData, so exercise the service after the controller's normalization.
        model.SubjectIds = model.SubjectIds.Distinct().ToList();
        var service = f.Get<IAdminTeacherService>();
        Assert.True((await service.CreateAsync(model)).Success);
        var user = await f.Db.Users.SingleAsync(u => u.Email == "new@test.local");
        Assert.Equal(UserRole.Teacher, user.Role);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.NotNull(user.EmailVerifiedAt);
        Assert.NotEqual(model.Password, user.PasswordHash);
        Assert.NotEqual(PasswordVerificationResult.Failed, f.Get<IPasswordHasher<AppUser>>().VerifyHashedPassword(user, user.PasswordHash, model.Password));
        var teacher = await f.Db.TeacherProfiles.Include(t => t.TeacherSubjects).SingleAsync(t => t.UserId == user.Id);
        Assert.Equal("Trần Bình", teacher.FullName);
        Assert.Single(teacher.TeacherSubjects);
        var duplicate = await service.CreateAsync(model);
        Assert.Equal(new AdminValidationError("Email", "Email này đã được sử dụng."), Assert.Single(duplicate.Errors));
    }

    [Fact]
    public async Task Teacher_profile_failure_rolls_back_new_user()
    {
        var failure = new FailSaveInterceptor();
        await using var f = await AdminTestDatabase.CreateAsync(failure);
        var (subject, _, _) = await f.SeedAsync();
        failure.FailAfter(2);
        var result = await f.Get<IAdminTeacherService>().CreateAsync(new()
        {
            Email = "rollback@test.local", FullName = "Teacher", Password = "StrongPassword1!", SubjectIds = new() { subject.Id }
        });
        Assert.False(result.Success);
        f.Db.ChangeTracker.Clear();
        Assert.False(await f.Db.Users.AnyAsync(u => u.Email == "rollback@test.local"));
        Assert.Equal(1, await f.Db.TeacherProfiles.CountAsync());
    }

    [Fact]
    public async Task Removing_teacher_subject_stays_blocked_until_class_is_finished()
    {
        await using var f = await AdminTestDatabase.CreateAsync();
        var (subject, teacher, slot) = await f.SeedAsync();
        var otherSubject = new Subject { Name = "Lý" };
        f.Db.Add(otherSubject);
        await f.Db.SaveChangesAsync();
        await f.Get<IAdminClassService>().CreateAsync(f.ClassForm(subject, slot, teacher));
        var service = f.Get<IAdminTeacherService>();
        var model = new AssignTeacherSubjectsViewModel { TeacherId = teacher.Id, SubjectIds = new() { otherSubject.Id } };
        var blocked = await service.AssignSubjectsAsync(model, true);
        Assert.False(blocked.Success);
        Assert.Contains("MAT-A1", Assert.Single(blocked.Errors).Message);
        var cls = await f.Db.Classes.SingleAsync();
        cls.Status = ClassStatus.Completed;
        await f.Db.SaveChangesAsync();
        Assert.True((await service.AssignSubjectsAsync(model, true)).Success);
        Assert.Equal(otherSubject.Id, (await f.Db.TeacherSubjects.SingleAsync()).SubjectId);
    }

    [Fact]
    public async Task Teacher_can_keep_existing_inactive_subject_but_cannot_add_another_inactive_subject()
    {
        await using var f = await AdminTestDatabase.CreateAsync();
        var (subject, teacher, _) = await f.SeedAsync();
        subject.Status = SubjectStatus.Inactive;
        var other = new Subject { Name = "Môn cũ", Status = SubjectStatus.Inactive };
        f.Db.Add(other);
        await f.Db.SaveChangesAsync();
        var service = f.Get<IAdminTeacherService>();
        Assert.True((await service.AssignSubjectsAsync(new() { TeacherId = teacher.Id, SubjectIds = new() { subject.Id } }, true)).Success);
        Assert.False((await service.AssignSubjectsAsync(new() { TeacherId = teacher.Id, SubjectIds = new() { subject.Id, other.Id } }, true)).Success);
    }

    [Fact]
    public async Task Teacher_index_keeps_vietnamese_given_name_sort_and_pagination()
    {
        await using var f = await AdminTestDatabase.CreateAsync();
        await f.SeedAsync();
        f.Db.TeacherProfiles.AddRange(
            new TeacherProfile { FullName = "Trần Bình", User = new() { Email = "binh@test.local", Role = UserRole.Teacher } },
            new TeacherProfile { FullName = "Lê An", User = new() { Email = "an@test.local", Role = UserRole.Teacher } });
        await f.Db.SaveChangesAsync();
        var data = await f.Get<IAdminTeacherService>().GetIndexAsync(999, null);
        Assert.Equal(new[] { "Lê An", "Trần Bình", "Nguyễn Lan" }, data.Teachers.Select(t => t.FullName));
        Assert.Equal(1, data.Pagination.Page);
        Assert.Equal(3, data.Pagination.TotalCount);
    }

    [Fact]
    public async Task Dashboard_keeps_existing_upcoming_filter_counts_and_six_class_limit()
    {
        await using var f = await AdminTestDatabase.CreateAsync();
        var (subject, teacher, _) = await f.SeedAsync();
        var today = DateOnly.FromDateTime(DateTime.Now);
        for (int i = 0; i < 8; i++)
            f.Db.Classes.Add(new()
            {
                Name = $"Open-{8 - i}", SubjectId = subject.Id, TeacherId = teacher.Id, Status = ClassStatus.Open,
                StartDate = today.AddDays(i), EndDate = today.AddDays(i + 30), MaxStudents = 20
            });
        f.Db.Classes.AddRange(
            new Class { Name = "Overdue", SubjectId = subject.Id, Status = ClassStatus.Open, StartDate = today.AddDays(-2), EndDate = today.AddDays(20), MaxStudents = 20 },
            new Class { Name = "Draft", SubjectId = subject.Id, Status = ClassStatus.Draft, StartDate = today, EndDate = today.AddDays(20), MaxStudents = 20 },
            new Class { Name = "Cancelled", SubjectId = subject.Id, Status = ClassStatus.Cancelled, StartDate = today, EndDate = today.AddDays(20), MaxStudents = 20 });
        await f.Db.SaveChangesAsync();
        var cls = await f.Db.Classes.SingleAsync(c => c.Name == "Open-8");
        await f.EnrollAsync(cls, EnrollmentStatus.Confirm);
        await f.EnrollAsync(cls, EnrollmentStatus.PendingPayment, DateTime.UtcNow.AddMinutes(10));
        await f.EnrollAsync(cls, EnrollmentStatus.PendingPayment, DateTime.UtcNow.AddMinutes(-10));
        var data = await f.Get<IAdminDashboardService>().GetDashboardAsync();
        Assert.Equal(6, data.UpcomingClasses.Count);
        Assert.Equal(Enumerable.Range(0, 6).Select(i => today.AddDays(i)), data.UpcomingClasses.Select(c => c.StartDate));
        Assert.DoesNotContain(data.UpcomingClasses, c => c.Name is "Overdue" or "Draft" or "Cancelled");
        Assert.Equal(9, data.OpenClasses);
        Assert.Equal(10, data.TotalClasses);
        Assert.Equal(1, data.CancelledClasses);
        Assert.Equal(2, data.OccupiedSeats);
        Assert.Equal(198, data.AvailableSeats);
        Assert.Equal(1, data.UpcomingClasses[0].Confirmed);
        Assert.Equal(1, data.UpcomingClasses[0].Reserved);
    }

    [Theory]
    [InlineData(typeof(SubjectsController))]
    [InlineData(typeof(TimeSlotsController))]
    [InlineData(typeof(TeachersController))]
    [InlineData(typeof(ClassesController))]
    [InlineData(typeof(AdminController))]
    public void Admin_authorization_and_antiforgery_are_preserved(Type controller)
    {
        Assert.Equal("Admin", Assert.Single(controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).Roles);
        foreach (var method in controller.GetMethods().Where(m => m.IsDefined(typeof(HttpPostAttribute), true)))
            Assert.True(method.IsDefined(typeof(ValidateAntiForgeryTokenAttribute), true), method.Name);
    }
}
