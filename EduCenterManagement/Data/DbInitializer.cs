
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(
            IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            var configuration = scope.ServiceProvider
                .GetRequiredService<IConfiguration>();

            var passwordHasher = scope.ServiceProvider
                .GetRequiredService<IPasswordHasher<AppUser>>();

            // Kiểm tra database đã được tạo bằng Migration chưa.
            if (!await context.Database.CanConnectAsync())
            {
                throw new InvalidOperationException(
                    "Không kết nối được database. Hãy kiểm tra SQL Server " +
                    "và chạy Update-Database trước.");
            }

            // Nếu đã có Admin thì không tạo thêm.
            bool adminExists = await context.Users
                .AnyAsync(u => u.Role == UserRole.Admin);

            if (adminExists)
                return;

            string? email = configuration["InitialAdmin:Email"];
            string? password = configuration["InitialAdmin:Password"];

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "Hãy cấu hình InitialAdmin:Email và " +
                    "InitialAdmin:Password bằng User Secrets.");
            }

            email = email.Trim();

            if (password.Length < 8)
            {
                throw new InvalidOperationException(
                    "Mật khẩu Admin ban đầu phải có ít nhất 8 ký tự.");
            }

            bool emailExists = await context.Users
                .AnyAsync(u => u.Email == email);

            if (emailExists)
            {
                throw new InvalidOperationException(
                    "Email này đã được sử dụng cho tài khoản khác.");
            }

            var admin = new AppUser
            {
                Email = email,
                Role = UserRole.Admin,
                Status = UserStatus.Active,
                EmailVerifiedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            admin.PasswordHash = passwordHasher.HashPassword(
                admin, password);

            context.Users.Add(admin);

            await context.SaveChangesAsync();
        }
    }
}
