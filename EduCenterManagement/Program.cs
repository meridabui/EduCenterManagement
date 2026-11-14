using EduCenterManagement.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using EduCenterManagement.Models;
using EduCenterManagement.Services;
using EduCenterManagement.Services.Admin;
using EduCenterManagement.Repositories.Admin;
using EduCenterManagement.Services.Email;
using EduCenterManagement.Services.Reminders;
using Microsoft.EntityFrameworkCore;
using EduCenterManagement.Services.Account;
using EduCenterManagement.Repositories.Account;
using EduCenterManagement.Repositories.Enrollments;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

// SMTP: gửi email xác thực, xác nhận đăng ký lớp...
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<IAppEmailSender, SmtpEmailSender>();

builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.Configure<VnPaySettings>(builder.Configuration.GetSection(VnPaySettings.SectionName));
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAdminUnitOfWork, AdminUnitOfWork>();
builder.Services.AddScoped<IAdminSubjectRepository, AdminSubjectRepository>();
builder.Services.AddScoped<IAdminSubjectService, AdminSubjectService>();
builder.Services.AddScoped<IAdminTimeSlotRepository, AdminTimeSlotRepository>();
builder.Services.AddScoped<IAdminTimeSlotService, AdminTimeSlotService>();
builder.Services.AddScoped<IAdminTeacherRepository, AdminTeacherRepository>();
builder.Services.AddScoped<IAdminTeacherService, AdminTeacherService>();
builder.Services.AddScoped<IAdminClassRepository, AdminClassRepository>();
builder.Services.AddScoped<IClassSessionService, ClassSessionService>();
builder.Services.AddScoped<IClassLifecycleService, ClassLifecycleService>();
builder.Services.AddScoped<IAdminClassService, AdminClassService>();
builder.Services.AddScoped<IAdminDashboardRepository, AdminDashboardRepository>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddHostedService<EnrollmentExpiryWorker>();

// Lập lịch và gửi email nhắc buổi học cho học sinh trước 24 giờ.
builder.Services.Configure<ClassReminderSettings>(
    builder.Configuration.GetSection(ClassReminderSettings.SectionName));
builder.Services.AddScoped<ClassReminderPlannerService>();
builder.Services.AddScoped<EmailOutboxProcessor>();
builder.Services.AddSingleton(new EnrollmentConfirmationEmailRuntime(DateTime.UtcNow));
builder.Services.AddScoped<EnrollmentConfirmationPlannerService>();
builder.Services.AddScoped<EnrollmentConfirmationEmailProcessor>();
builder.Services.AddHostedService<ClassReminderPlannerWorker>();
builder.Services.AddHostedService<EmailOutboxWorker>();
// Đăng ký Repository & Service cho phân hệ Student
builder.Services.AddScoped<EduCenterManagement.Repositories.Student.IStudentTimetableRepository, EduCenterManagement.Repositories.Student.StudentTimetableRepository>();
builder.Services.AddScoped<EduCenterManagement.Services.Student.IStudentTimetableService, EduCenterManagement.Services.Student.StudentTimetableService>();
builder.Services.AddScoped<EduCenterManagement.Repositories.Student.IStudentQuizRepository, EduCenterManagement.Repositories.Student.StudentQuizRepository>();
builder.Services.AddScoped<EduCenterManagement.Services.Student.IStudentQuizService, EduCenterManagement.Services.Student.StudentQuizService>();
builder.Services.AddScoped<EduCenterManagement.Repositories.Student.IStudentCommentRepository, EduCenterManagement.Repositories.Student.StudentCommentRepository>();
builder.Services.AddScoped<EduCenterManagement.Services.Student.IStudentCommentService, EduCenterManagement.Services.Student.StudentCommentService>();

// Đăng ký Repository & Service cho phân hệ Teacher
builder.Services.AddScoped<EduCenterManagement.Repositories.Teacher.ITeacherRepository, EduCenterManagement.Repositories.Teacher.TeacherRepository>();
builder.Services.AddScoped<EduCenterManagement.Services.Teacher.ITeacherService, EduCenterManagement.Services.Teacher.TeacherService>();
builder.Services.AddScoped<EduCenterManagement.Repositories.Teacher.ITeacherQuizRepository, EduCenterManagement.Repositories.Teacher.TeacherQuizRepository>();
builder.Services.AddScoped<EduCenterManagement.Services.Teacher.ITeacherQuizService, EduCenterManagement.Services.Teacher.TeacherQuizService>();
builder.Services.AddScoped<EduCenterManagement.Repositories.Teacher.ITeacherAttendanceRepository, EduCenterManagement.Repositories.Teacher.TeacherAttendanceRepository>();
builder.Services.AddScoped<EduCenterManagement.Services.Teacher.ITeacherAttendanceService, EduCenterManagement.Services.Teacher.TeacherAttendanceService>();
builder.Services.AddScoped<EduCenterManagement.Repositories.Teacher.ITeacherSlideRepository, EduCenterManagement.Repositories.Teacher.TeacherSlideRepository>();
builder.Services.AddScoped<EduCenterManagement.Services.Teacher.ITeacherSlideService, EduCenterManagement.Services.Teacher.TeacherSlideService>();
builder.Services.AddScoped<EduCenterManagement.Repositories.Teacher.ITeacherCommentRepository, EduCenterManagement.Repositories.Teacher.TeacherCommentRepository>();
builder.Services.AddScoped<EduCenterManagement.Services.Teacher.ITeacherCommentService, EduCenterManagement.Services.Teacher.TeacherCommentService>();

// Dữ liệu học tập chỉ đọc dành cho phụ huynh.
builder.Services.AddScoped<EduCenterManagement.Repositories.Parent.IParentAcademicRepository, EduCenterManagement.Repositories.Parent.ParentAcademicRepository>();
builder.Services.AddScoped<EduCenterManagement.Services.Parent.IParentAcademicService, EduCenterManagement.Services.Parent.ParentAcademicService>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

//cookie authentication
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";

        options.Cookie.Name = "EduCenterManagement.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();



// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();
await DbInitializer.InitializeAsync(app.Services);//tạo admin khi ứng dụng khởi động

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
// Xác thực người dùng trước
app.UseAuthentication();
// Kiểm tra quyền truy cập sau
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
