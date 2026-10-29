using EduCenterManagement.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using EduCenterManagement.Models;
using EduCenterManagement.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

// SMTP: gửi email xác thực, xác nhận đăng ký lớp...
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<IAppEmailSender, SmtpEmailSender>();

builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddHostedService<EnrollmentExpiryWorker>();
// Đăng ký Repository & Service cho phân hệ Student
builder.Services.AddScoped<EduCenterManagement.Repositories.Student.IStudentTimetableRepository, EduCenterManagement.Repositories.Student.StudentTimetableRepository>();
builder.Services.AddScoped<EduCenterManagement.Services.Student.IStudentTimetableService, EduCenterManagement.Services.Student.StudentTimetableService>();

// Đăng ký Repository & Service cho phân hệ Teacher
builder.Services.AddScoped<EduCenterManagement.Repositories.Teacher.ITeacherRepository, EduCenterManagement.Repositories.Teacher.TeacherRepository>();
builder.Services.AddScoped<EduCenterManagement.Services.Teacher.ITeacherService, EduCenterManagement.Services.Teacher.TeacherService>();
builder.Services.AddScoped<EduCenterManagement.Repositories.Teacher.ITeacherQuizRepository, EduCenterManagement.Repositories.Teacher.TeacherQuizRepository>();
builder.Services.AddScoped<EduCenterManagement.Services.Teacher.ITeacherQuizService, EduCenterManagement.Services.Teacher.TeacherQuizService>();

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
