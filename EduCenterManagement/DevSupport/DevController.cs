// TEMP: remove before merge to main
using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.DevSupport;

[Route("Dev")]
public sealed class DevController(ApplicationDbContext db, IWebHostEnvironment environment) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!environment.IsDevelopment()) return NotFound();
        return View(await db.Users.AsNoTracking().Where(x => x.Email.StartsWith("seed.")).OrderBy(x => x.Id).ToListAsync(ct));
    }

    [HttpGet("Seed")]
    public async Task<IActionResult> Seed(CancellationToken ct)
    {
        if (!environment.IsDevelopment()) return NotFound();
        if (await db.Users.AnyAsync(x => x.Email == "seed.parent@ecm.test", ct)) return RedirectToAction(nameof(Index));
        var now=DateTime.UtcNow;
        var users=new[]{new AppUser{Email="seed.admin@ecm.test",PasswordHash="seed",Role=UserRole.Admin},new AppUser{Email="seed.teacher1@ecm.test",PasswordHash="seed",Role=UserRole.Teacher},new AppUser{Email="seed.teacher2@ecm.test",PasswordHash="seed",Role=UserRole.Teacher},new AppUser{Email="seed.parent@ecm.test",PasswordHash="seed",Role=UserRole.Parent},new AppUser{Email="seed.student1@ecm.test",PasswordHash="seed",Role=UserRole.Student},new AppUser{Email="seed.student2@ecm.test",PasswordHash="seed",Role=UserRole.Student},new AppUser{Email="seed.student3@ecm.test",PasswordHash="seed",Role=UserRole.Student}};
        db.Users.AddRange(users);await db.SaveChangesAsync(ct);
        var teachers=new[]{new TeacherProfile{UserId=users[1].Id,FullName="Alex Teacher"},new TeacherProfile{UserId=users[2].Id,FullName="Taylor Teacher"}};var parent=new ParentProfile{UserId=users[3].Id,FullName="Pat Parent",LinkingCode="SEED-PARENT"};var students=new[]{new StudentProfile{UserId=users[4].Id,FullName="Sam Student"},new StudentProfile{UserId=users[5].Id,FullName="Casey Student"},new StudentProfile{UserId=users[6].Id,FullName="Jordan Student"}};db.AddRange(teachers);db.Add(parent);db.AddRange(students);await db.SaveChangesAsync(ct);
        db.ParentStudentLinks.AddRange(new ParentStudentLink{ParentId=parent.Id,StudentId=students[0].Id,Status=ParentStudentLinkStatus.Accepted},new ParentStudentLink{ParentId=parent.Id,StudentId=students[1].Id,Status=ParentStudentLinkStatus.Accepted});var subjects=new[]{new Subject{Name="Mathematics"},new Subject{Name="Physics"}};db.AddRange(subjects);await db.SaveChangesAsync(ct);
        var start=DateOnly.FromDateTime(DateTime.Today).AddDays(-14);var classes=new[]{new Class{SubjectId=subjects[0].Id,TeacherId=teachers[0].Id,Name="Math A",StartDate=start,EndDate=start.AddDays(90),MaxStudents=30,Fee=1000000,Status=ClassStatus.InProgress},new Class{SubjectId=subjects[1].Id,TeacherId=teachers[1].Id,Name="Physics B",StartDate=start,EndDate=start.AddDays(90),MaxStudents=30,Fee=1000000,Status=ClassStatus.InProgress},new Class{SubjectId=subjects[0].Id,TeacherId=teachers[0].Id,Name="Math Schedule Fallback",StartDate=start,EndDate=start.AddDays(90),MaxStudents=30,Fee=1000000,Status=ClassStatus.Open}};db.AddRange(classes);var slots=new[]{new TimeSlot{Name="Slot 1",StartTime=new TimeOnly(8,0),EndTime=new TimeOnly(9,30),Status=TimeSlotStatus.Active},new TimeSlot{Name="Slot 2",StartTime=new TimeOnly(18,0),EndTime=new TimeOnly(19,30),Status=TimeSlotStatus.Active}};db.AddRange(slots);await db.SaveChangesAsync(ct);
        db.ClassSchedules.AddRange(new ClassSchedule{ClassId=classes[0].Id,TimeSlotId=slots[0].Id,DayOfWeek=2},new ClassSchedule{ClassId=classes[1].Id,TimeSlotId=slots[1].Id,DayOfWeek=4},new ClassSchedule{ClassId=classes[2].Id,TimeSlotId=slots[1].Id,DayOfWeek=6});db.Enrollments.AddRange(new Enrollment{StudentId=students[0].Id,ClassId=classes[0].Id,RegisteredByParentId=parent.Id,Status=EnrollmentStatus.Confirmed},new Enrollment{StudentId=students[0].Id,ClassId=classes[2].Id,RegisteredByParentId=parent.Id,Status=EnrollmentStatus.Confirmed},new Enrollment{StudentId=students[1].Id,ClassId=classes[1].Id,RegisteredByParentId=parent.Id,Status=EnrollmentStatus.Confirmed});await db.SaveChangesAsync(ct);
        var monday=DateOnly.FromDateTime(DateTime.Today).AddDays(-((int)DateTime.Today.DayOfWeek+6)%7);var session=new ClassSession{ClassId=classes[0].Id,SessionDate=monday,StartsAt=new TimeOnly(8,0),EndsAt=new TimeOnly(9,30),SessionNumber=1,Status=ClassSessionStatus.Completed};db.Add(session);await db.SaveChangesAsync(ct);db.AttendanceRecords.AddRange(new AttendanceRecord{SessionId=session.Id,StudentId=students[0].Id,MarkedByTeacherId=teachers[0].Id,Status=AttendanceStatus.Present},new AttendanceRecord{SessionId=session.Id,StudentId=students[1].Id,MarkedByTeacherId=teachers[0].Id,Status=AttendanceStatus.Absent});db.TeacherComments.Add(new TeacherComment{TeacherId=teachers[0].Id,StudentId=students[0].Id,ClassId=classes[0].Id,Rating=5,Content="Excellent progress."});db.Notifications.Add(new Notification{UserId=users[3].Id,StudentId=students[0].Id,Type="teacher_comment",Title="New teacher comment",Message="Alex Teacher left a comment for Sam Student."});await db.SaveChangesAsync(ct);return RedirectToAction(nameof(Index));
    }
}
