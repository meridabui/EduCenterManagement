
using System.Security.Claims;
using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.ViewModels.Account;
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
            bool isAjax = string.Equals(
                Request.Headers["X-Requested-With"].ToString(),
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase);

            IActionResult LoginError(string message)
            {
                if (isAjax)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message
                    });
                }

                ModelState.AddModelError(string.Empty, message);
                ViewBag.ReturnUrl = returnUrl;
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                if (isAjax)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Vui lòng kiểm tra email và mật khẩu."
                    });
                }

                ViewBag.ReturnUrl = returnUrl;
                return View(model);
            }

            var email = model.Email.Trim();

            var user = await _context.Users
                .SingleOrDefaultAsync(u => u.Email == email);

            if (user == null || user.Status != UserStatus.Active)
            {
                return LoginError(
                    "Email hoặc mật khẩu không chính xác.");
            }

            var verificationResult =
                _passwordHasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash,
                    model.Password);

            if (verificationResult == PasswordVerificationResult.Failed)
            {
                return LoginError(
                    "Email hoặc mật khẩu không chính xác.");
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

            var portalUrl = GetPortalUrl(user.Role);

            if (!string.IsNullOrWhiteSpace(returnUrl) &&
                Url.IsLocalUrl(returnUrl))
            {
                portalUrl = returnUrl;
            }

            var roleName = user.Role switch
            {
                UserRole.Admin => "Admin",
                UserRole.Teacher => "Giáo viên",
                UserRole.Parent => "Phụ huynh",
                UserRole.Student => "Học sinh",
                _ => "Người dùng"
            };

            if (isAjax)
            {
                return Json(new
                {
                    success = true,
                    message = "Đăng nhập thành công.",
                    name = user.Email,
                    role = user.Role.ToString(),
                    roleName,
                    portalUrl
                });
            }

            return LocalRedirect(
                Url.IsLocalUrl(portalUrl) ? portalUrl : "/");
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

        private static string GetPortalUrl(UserRole role)
        {
            return role switch
            {
                UserRole.Admin => "/Admin",
                UserRole.Teacher => "/Teacher",
                UserRole.Parent => "/ParentDashboard",
                UserRole.Student => "/StudentDashboard",
                _ => "/"
            };
        }
    }
}
