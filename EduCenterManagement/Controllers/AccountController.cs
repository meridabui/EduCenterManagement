
using System.Security.Claims;
using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<AppUser> _passwordHasher;

        public AccountController(
            ApplicationDbContext context,
            IPasswordHasher<AppUser> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model,
            string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
                return View(model);

            var email = model.Email.Trim();

            var user = await _context.Users
                .SingleOrDefaultAsync(u => u.Email == email);

            if (user == null || user.Status != UserStatus.Active)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Email hoặc mật khẩu không chính xác.");

                return View(model);
            }

            var verificationResult = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                model.Password);

            if (verificationResult == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Email hoặc mật khẩu không chính xác.");

                return View(model);
            }

            if (verificationResult ==
                PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _passwordHasher.HashPassword(
                    user,
                    model.Password);

                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    AllowRefresh = true
                });

            if (!string.IsNullOrWhiteSpace(returnUrl) &&
                Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }


            switch (user.Role) //xử lý returnUrl hợp lệ hiện có, để các yêu cầu chuyển hướng nội bộ vẫn hoạt động.
            {
                case UserRole.Admin:
                    return RedirectToAction("Index", "Admin");

                case UserRole.Teacher:
                    return RedirectToAction("Index", "Teacher");

                case UserRole.Parent:
                    return RedirectToAction("Index", "ParentDashboard");

                case UserRole.Student:
                    return RedirectToAction("Index", "StudentDashboard");

                default:
                    await HttpContext.SignOutAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme);

                    return RedirectToAction(nameof(Login));
            }

        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction(nameof(Login));
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
