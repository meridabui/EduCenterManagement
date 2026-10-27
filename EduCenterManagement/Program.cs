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
builder.Services.AddPart5Services();
builder.Services.AddPart5Results();
builder.Services.AddPart5Comments();
builder.Services.AddPart5TeacherComments();

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

if (app.Environment.IsDevelopment())
    app.MapControllerRoute(name: "dev", pattern: "Dev/{action=Index}/{id?}", defaults: new { controller = "Dev" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
