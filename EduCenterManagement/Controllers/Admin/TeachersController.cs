
using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Controllers.Admin
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
                .AsNoTracking()
                .Include(t => t.User)
                .OrderBy(t => t.FullName)
                .ToListAsync();

            return View("~/Views/Admin/Teachers/Index.cshtml", teachers);
        }

        // GET: /Teachers/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View(
                "~/Views/Admin/Teachers/Create.cshtml",
                new CreateTeacherViewModel());
        }

        // POST: /Teachers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            CreateTeacherViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(
                    "~/Views/Admin/Teachers/Create.cshtml", model);
            }

            var email = model.Email.Trim().ToLowerInvariant();
            var fullName = model.FullName.Trim();

            bool emailExists = await _context.Users
                .AnyAsync(u => u.Email == email);

            if (emailExists)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "Email này đã được sử dụng.");

                return View(
                    "~/Views/Admin/Teachers/Create.cshtml", model);
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var now = DateTime.UtcNow;

                var user = new AppUser
                {
                    Email = email,
                    Role = UserRole.Teacher,
                    Status = UserStatus.Active,

                    // Chỉ nên đánh dấu xác thực nếu quy trình
                    // tạo tài khoản của nhóm cho phép điều đó.
                    EmailVerifiedAt = null,

                    CreatedAt = now,
                    UpdatedAt = now
                };

                user.PasswordHash = _passwordHasher.HashPassword(
                    user, model.Password);

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var teacher = new TeacherProfile
                {
                    UserId = user.Id,
                    FullName = fullName,
                    CreatedAt = now,
                    UpdatedAt = now
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
                    "Không thể tạo tài khoản Teacher. " +
                    "Email có thể đã tồn tại hoặc dữ liệu không hợp lệ.");

                return View(
                    "~/Views/Admin/Teachers/Create.cshtml", model);
            }
        }
    }
}
