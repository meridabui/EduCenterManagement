
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.Services;
using EduCenterManagement.ViewModels.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<AppUser> _passwordHasher;
        private readonly IAppEmailSender _emailSender;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            ApplicationDbContext context,
            IPasswordHasher<AppUser> passwordHasher,
            IAppEmailSender emailSender,
            ILogger<AccountController> logger)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _emailSender = emailSender;
            _logger = logger;
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

            if (user == null)
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

            // BR01: email phải được xác thực trước khi đăng nhập.
            if (user.Status == UserStatus.PendingVerification)
            {
                const string pendingMessage =
                    "Tài khoản chưa được xác thực email. " +
                    "Vui lòng kiểm tra hộp thư hoặc gửi lại email xác thực.";

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

            if (user.Status != UserStatus.Active)
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

            // Chỉ Parent và Student được tự đăng ký (BR02).
            if (!Enum.TryParse<UserRole>(model.Role, true, out var role) ||
                (role != UserRole.Parent && role != UserRole.Student))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Vai trò không hợp lệ."
                });
            }

            var email = model.Email.Trim();
            var fullName = model.FullName.Trim();

            // BR01: mỗi tài khoản có một email duy nhất.
            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                return Conflict(new
                {
                    success = false,
                    message = "Email này đã được sử dụng."
                });
            }

            var now = DateTime.UtcNow;

            var user = new AppUser
            {
                Email = email,
                Role = role,
                Status = UserStatus.PendingVerification,
                CreatedAt = now,
                UpdatedAt = now
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

            if (role == UserRole.Parent)
            {
                // Cột LinkingCode là NOT NULL + UNIQUE nên tạm dùng mã chờ.
                // Mã thật được sinh sau khi xác thực email (BR03).
                _context.ParentProfiles.Add(new ParentProfile
                {
                    User = user,
                    FullName = fullName,
                    LinkingCode = "PENDING-" + Guid.NewGuid().ToString("N"),
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            else
            {
                _context.StudentProfiles.Add(new StudentProfile
                {
                    User = user,
                    FullName = fullName,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            var (rawToken, tokenHash) = CreateVerificationToken();

            _context.EmailVerificationTokens.Add(new EmailVerificationToken
            {
                User = user,
                TokenHash = tokenHash,
                ExpiresAt = now.AddHours(24),
                CreatedAt = now
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Register failed for {Email}", email);

                return Conflict(new
                {
                    success = false,
                    message = "Không thể tạo tài khoản. Email có thể đã được sử dụng."
                });
            }

            var sent = await TrySendVerificationEmailAsync(fullName, email, rawToken);

            var message = sent
                ? "Tài khoản đã được tạo. Vui lòng kiểm tra email để xác thực " +
                  "trước khi đăng nhập (liên kết có hiệu lực 24 giờ)."
                : "Tài khoản đã được tạo nhưng chưa gửi được email xác thực. " +
                  "Hãy đăng nhập và chọn \"Gửi lại email xác thực\".";

            return Json(new { success = true, emailSent = sent, message });
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> VerifyEmail(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return VerifyEmailResult(false, "Liên kết xác thực không hợp lệ.");
            }

            var tokenHash = HashToken(token);

            var record = await _context.EmailVerificationTokens
                .Include(t => t.User)
                .SingleOrDefaultAsync(t => t.TokenHash == tokenHash);

            if (record == null)
            {
                return VerifyEmailResult(false, "Liên kết xác thực không hợp lệ.");
            }

            if (record.UsedAt != null)
            {
                return VerifyEmailResult(
                    false,
                    "Liên kết này đã được sử dụng. Nếu đã xác thực, bạn có thể đăng nhập.");
            }

            if (record.ExpiresAt < DateTime.UtcNow)
            {
                return VerifyEmailResult(
                    false,
                    "Liên kết đã hết hạn. Hãy đăng nhập và chọn \"Gửi lại email xác thực\".");
            }

            var user = record.User;

            if (user.Status != UserStatus.PendingVerification &&
                user.Status != UserStatus.Active)
            {
                return VerifyEmailResult(false, "Tài khoản không thể xác thực.");
            }

            var now = DateTime.UtcNow;

            user.EmailVerifiedAt ??= now;
            user.Status = UserStatus.Active;
            user.UpdatedAt = now;
            record.UsedAt = now;

            // BR03: sinh Parent Linking Code sau khi email được xác thực.
            if (user.Role == UserRole.Parent)
            {
                var profile = await _context.ParentProfiles
                    .SingleOrDefaultAsync(p => p.UserId == user.Id);

                if (profile != null &&
                    profile.LinkingCode.StartsWith("PENDING-", StringComparison.Ordinal))
                {
                    profile.LinkingCode = await GenerateParentLinkingCodeAsync();
                    profile.UpdatedAt = now;
                }
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "VerifyEmail failed for user {UserId}", user.Id);

                return VerifyEmailResult(
                    false,
                    "Có lỗi khi xác thực. Vui lòng bấm lại liên kết trong email.");
            }

            return VerifyEmailResult(
                true,
                "Email đã được xác thực. Bạn có thể đăng nhập vào hệ thống.");
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendVerification(string? email)
        {
            const string genericMessage =
                "Nếu tài khoản tồn tại và chưa được xác thực, email xác thực mới " +
                "đã được gửi. Vui lòng kiểm tra hộp thư (mỗi lần gửi cách nhau 1 phút).";

            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Vui lòng nhập email ở form đăng nhập."
                });
            }

            email = email.Trim();

            var user = await _context.Users.SingleOrDefaultAsync(u =>
                u.Email == email &&
                u.Status == UserStatus.PendingVerification);

            if (user == null)
            {
                return Json(new { success = true, message = genericMessage });
            }

            var cutoff = DateTime.UtcNow.AddSeconds(-60);

            if (await _context.EmailVerificationTokens.AnyAsync(t =>
                    t.UserId == user.Id && t.CreatedAt > cutoff))
            {
                return Json(new { success = true, message = genericMessage });
            }

            var fullName = user.Role == UserRole.Parent
                ? await _context.ParentProfiles
                    .Where(p => p.UserId == user.Id)
                    .Select(p => p.FullName)
                    .FirstOrDefaultAsync()
                : await _context.StudentProfiles
                    .Where(s => s.UserId == user.Id)
                    .Select(s => s.FullName)
                    .FirstOrDefaultAsync();

            var (rawToken, tokenHash) = CreateVerificationToken();
            var now = DateTime.UtcNow;

            _context.EmailVerificationTokens.Add(new EmailVerificationToken
            {
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = now.AddHours(24),
                CreatedAt = now
            });

            await _context.SaveChangesAsync();

            await TrySendVerificationEmailAsync(
                fullName ?? user.Email,
                user.Email,
                rawToken);

            return Json(new { success = true, message = genericMessage });
        }

        // ================= HÀM HỖ TRỢ =================

        private IActionResult VerifyEmailResult(bool success, string message)
        {
            ViewData["Success"] = success;
            ViewData["Message"] = message;
            return View("VerifyEmail");
        }

        private static (string RawToken, string TokenHash) CreateVerificationToken()
        {
            var raw = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            return (raw, HashToken(raw));
        }

        // Chỉ lưu hash của token trong database.
        private static string HashToken(string rawToken)
        {
            return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        }

        // Non-UI function: GenerateParentLinkingCode. Định dạng PAR-2026-ABC123.
        private async Task<string> GenerateParentLinkingCodeAsync()
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

            for (var attempt = 0; attempt < 10; attempt++)
            {
                var chars = new char[6];

                for (var i = 0; i < chars.Length; i++)
                {
                    chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
                }

                var code = $"PAR-{DateTime.UtcNow.Year}-{new string(chars)}";

                if (!await _context.ParentProfiles.AnyAsync(p => p.LinkingCode == code))
                {
                    return code;
                }
            }

            throw new InvalidOperationException(
                "Không tạo được Parent Linking Code duy nhất.");
        }

        // Non-UI function: SendEmailVerification.
        private async Task<bool> TrySendVerificationEmailAsync(
            string fullName,
            string toEmail,
            string rawToken)
        {
            try
            {
                var link = Url.Action(
                    nameof(VerifyEmail),
                    "Account",
                    new { token = rawToken },
                    Request.Scheme);

                var body =
                    $"<p>Xin chào {WebUtility.HtmlEncode(fullName)},</p>" +
                    "<p>Cảm ơn bạn đã đăng ký tài khoản EduCenter. " +
                    "Vui lòng nhấn vào liên kết bên dưới để xác thực email " +
                    "(có hiệu lực trong 24 giờ):</p>" +
                    $"<p><a href=\"{link}\">Xác thực email</a></p>" +
                    "<p>Nếu bạn không đăng ký tài khoản này, hãy bỏ qua email.</p>";

                await _emailSender.SendAsync(
                    toEmail,
                    "Xác thực email tài khoản EduCenter",
                    body);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification email to {Email}", toEmail);
                return false;
            }
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
