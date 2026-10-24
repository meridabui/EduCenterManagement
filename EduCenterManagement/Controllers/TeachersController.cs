
using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class TeachersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<AppUser> _passwordHasher;

        public TeachersController(
            ApplicationDbContext context,
            IPasswordHasher<AppUser> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        // GET: /Teachers
        public async Task<IActionResult> Index()
        {
            var teachers = await _context.TeacherProfiles
                .Include(t => t.User)
                .AsNoTracking()
                .OrderBy(t => t.FullName)
                .ToListAsync();

            return View(teachers);
        }

        // GET: /Teachers/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateTeacherViewModel());
        }

        // POST: /Teachers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            CreateTeacherViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var email = model.Email.Trim().ToLowerInvariant();

            bool emailExists = await _context.Users
                .AnyAsync(u => u.Email == email);

            if (emailExists)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "Email này đã được sử dụng.");

                return View(model);
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var user = new AppUser
                {
                    Email = email,
                    Role = UserRole.Teacher,
                    Status = UserStatus.Active,
                    EmailVerifiedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                user.PasswordHash = _passwordHasher.HashPassword(
                    user, model.Password);

                _context.Users.Add(user);

                // Lưu để lấy User.Id
                await _context.SaveChangesAsync();

                var teacher = new TeacherProfile
                {
                    UserId = user.Id,
                    FullName = model.FullName.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.TeacherProfiles.Add(teacher);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    "Tạo tài khoản Teacher thành công.";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "Không thể tạo tài khoản. Email có thể đã tồn tại. " +
                    "Vui lòng kiểm tra lại.");

                return View(model);
            }
        }
    }
}
