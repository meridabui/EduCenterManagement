
using System.Security.Claims;
using EduCenterManagement.Enums;
using EduCenterManagement.Services.Account;
using EduCenterManagement.ViewModels.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduCenterManagement.Controllers
{
    public class AccountController : Controller
    {
        private const string InvalidCredentialsMessage =
            "Email hoặc mật khẩu không chính xác.";

        private readonly IAccountService _accountService;

        public AccountController(IAccountService accountService)
        {
            _accountService = accountService;
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

            var result = await _accountService.AuthenticateAsync(
                model.Email,
                model.Password);

            if (result.Status == LoginStatus.InvalidCredentials)
            {
                return LoginError(InvalidCredentialsMessage);
            }

            // BR01: email phải được xác thực trước khi đăng nhập.
            if (result.Status == LoginStatus.PendingVerification)
            {
                const string pendingMessage =
                "Tài khoản chưa được xác thực email. Vui lòng nhập mã xác thực đã gửi đến email của bạn.";
                if (isAjax)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = pendingMessage,
                        needsVerification = true
                    });
                }

                ModelState.AddModelError(string.Empty, pendingMessage);
                ViewBag.ReturnUrl = returnUrl;
                return View(model);
            }

            var user = result.User!;

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

        // ================= ĐĂNG KÝ & XÁC THỰC EMAIL =================

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var firstError = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
                    ?? "Dữ liệu không hợp lệ.";

                return BadRequest(new { success = false, message = firstError });
            }

            var result = await _accountService.RegisterAsync(model);

            return result.Status switch
            {
                RegisterStatus.Success => Json(new
                {
                    success = true,
                    emailSent = result.EmailSent,
                    requiresCode = true,
                    email = model.Email.Trim(),
                    message = result.Message
                }),
            RegisterStatus.InvalidRole => BadRequest(new
                {
                    success = false,
                    message = result.Message
                }),
                _ => Conflict(new
                {
                    success = false,
                    message = result.Message
                })
            };
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmail(string? email, string? code)
        {
            var result = await _accountService.VerifyEmailAsync(email, code);

            return result.Success
                ? Json(new { success = true, message = result.Message })
                : BadRequest(new { success = false, message = result.Message });
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendVerification(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Vui lòng nhập email ở form đăng nhập."
                });
            }

            var message = await _accountService.ResendVerificationAsync(
                email);

            return Json(new { success = true, message });
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
