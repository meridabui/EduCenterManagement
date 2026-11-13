using System.Net;
using System.Security.Cryptography;
using System.Text;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.Repositories.Account;
using EduCenterManagement.ViewModels.Account;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Services.Account;

public sealed class AccountService : IAccountService
{
    // Mã xác thực 6 số có hiệu lực 10 phút, tối đa 5 lần nhập sai.
    private const int CodeLifetimeMinutes = 10;
    private const int MaxFailedAttempts = 5;

    private const string ResendGenericMessage =
        "Nếu tài khoản tồn tại và chưa được xác thực, mã xác thực mới " +
        "đã được gửi. Vui lòng kiểm tra hộp thư (mỗi lần gửi cách nhau 1 phút).";

    private readonly IAccountRepository _repository;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly IAppEmailSender _emailSender;
    private readonly TimeProvider _clock;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        IAccountRepository repository,
        IPasswordHasher<AppUser> passwordHasher,
        IAppEmailSender emailSender,
        TimeProvider clock,
        ILogger<AccountService> logger)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _emailSender = emailSender;
        _clock = clock;
        _logger = logger;
    }

    public async Task<LoginResult> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetUserByEmailAsync(
            email.Trim(),
            cancellationToken);

        if (user == null)
        {
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        // BR01: email phải được xác thực trước khi đăng nhập.
        if (user.Status == UserStatus.PendingVerification)
        {
            return new LoginResult(LoginStatus.PendingVerification);
        }

        if (user.Status != UserStatus.Active)
        {
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
            user.UpdatedAt = UtcNow();

            await _repository.SaveChangesAsync(cancellationToken);
        }

        return new LoginResult(LoginStatus.Success, user);
    }

    public async Task<RegisterResult> RegisterAsync(
        RegisterViewModel model,
        CancellationToken cancellationToken = default)
    {
        // Chỉ Parent và Student được tự đăng ký (BR02).
        if (!Enum.TryParse<UserRole>(model.Role, true, out var role) ||
            (role != UserRole.Parent && role != UserRole.Student))
        {
            return new RegisterResult(
                RegisterStatus.InvalidRole,
                "Vai trò không hợp lệ.");
        }

        var email = model.Email.Trim();
        var fullName = model.FullName.Trim();

        // BR01: mỗi tài khoản có một email duy nhất.
        if (await _repository.EmailExistsAsync(email, cancellationToken))
        {
            return new RegisterResult(
                RegisterStatus.EmailTaken,
                "Email này đã được sử dụng.");
        }

        var now = UtcNow();

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
            _repository.AddParentProfile(new ParentProfile
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
            _repository.AddStudentProfile(new StudentProfile
            {
                User = user,
                FullName = fullName,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        var code = GenerateVerificationCode();

        _repository.AddVerificationToken(new EmailVerificationToken
        {
            User = user,
            TokenHash = HashCode(email, code),
            ExpiresAt = now.AddMinutes(CodeLifetimeMinutes),
            CreatedAt = now
        });

        try
        {
            await _repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Register failed for {Email}", email);

            return new RegisterResult(
                RegisterStatus.SaveFailed,
                "Không thể tạo tài khoản. Email có thể đã được sử dụng.");
        }

        var sent = await TrySendVerificationEmailAsync(fullName, email, code);

        var message = sent
            ? "Tài khoản đã được tạo. Mã xác thực 6 số đã được gửi tới email " +
              $"của bạn (có hiệu lực {CodeLifetimeMinutes} phút)."
            : "Tài khoản đã được tạo nhưng chưa gửi được mã xác thực. " +
              "Hãy chọn \"Gửi lại mã\".";

        return new RegisterResult(RegisterStatus.Success, message, sent);
    }

    public async Task<EmailVerificationResult> VerifyEmailAsync(
        string? email,
        string? code,
        CancellationToken cancellationToken = default)
    {
        const string invalidMessage = "Mã xác thực không hợp lệ.";
        const string tooManyMessage =
            "Bạn đã nhập sai quá nhiều lần. " +
            "Hãy chọn \"Gửi lại mã\" để nhận mã mới.";

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(code) ||
            code.Length != 6 ||
            !code.All(char.IsAsciiDigit))
        {
            return new EmailVerificationResult(
                false,
                "Mã xác thực gồm đúng 6 chữ số.");
        }

        var user = await _repository.GetUserByEmailAsync(
            email.Trim(),
            cancellationToken);

        if (user == null)
        {
            return new EmailVerificationResult(false, invalidMessage);
        }

        if (user.Status == UserStatus.Active && user.EmailVerifiedAt != null)
        {
            return new EmailVerificationResult(
                false,
                "Tài khoản đã được xác thực. Bạn có thể đăng nhập.");
        }

        if (user.Status != UserStatus.PendingVerification)
        {
            return new EmailVerificationResult(
                false,
                "Tài khoản không thể xác thực.");
        }

        var now = UtcNow();

        // Chỉ mã mới nhất còn hiệu lực.
        var record = (await _repository.GetUnusedVerificationTokensAsync(
            user.Id,
            cancellationToken)).FirstOrDefault();

        if (record == null || record.ExpiresAt < now)
        {
            return new EmailVerificationResult(
                false,
                "Mã đã hết hạn. Hãy chọn \"Gửi lại mã\".");
        }

        if (record.FailedAttempts >= MaxFailedAttempts)
        {
            return new EmailVerificationResult(false, tooManyMessage);
        }

        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(record.TokenHash),
            Encoding.UTF8.GetBytes(HashCode(user.Email, code)));

        if (!matches)
        {
            record.FailedAttempts++;
            await _repository.SaveChangesAsync(cancellationToken);

            var remaining = MaxFailedAttempts - record.FailedAttempts;

            return new EmailVerificationResult(
                false,
                remaining > 0
                    ? $"Mã không đúng. Bạn còn {remaining} lần thử."
                    : tooManyMessage);
        }

        user.EmailVerifiedAt ??= now;
        user.Status = UserStatus.Active;
        user.UpdatedAt = now;
        record.UsedAt = now;

        // BR03: sinh Parent Linking Code sau khi email được xác thực.
        if (user.Role == UserRole.Parent)
        {
            var profile = await _repository.GetParentProfileByUserIdAsync(
                user.Id,
                cancellationToken);

            if (profile != null &&
                profile.LinkingCode.StartsWith("PENDING-", StringComparison.Ordinal))
            {
                profile.LinkingCode = await GenerateParentLinkingCodeAsync(
                    cancellationToken);
                profile.UpdatedAt = now;
            }
        }

        try
        {
            await _repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "VerifyEmail failed for user {UserId}", user.Id);

            return new EmailVerificationResult(
                false,
                "Có lỗi khi xác thực. Vui lòng thử lại.");
        }

        return new EmailVerificationResult(
            true,
            "Email đã được xác thực. Bạn có thể đăng nhập vào hệ thống.");
    }

    public async Task<string> ResendVerificationAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        email = email.Trim();

        var user = await _repository.GetPendingUserByEmailAsync(
            email,
            cancellationToken);

        if (user == null)
        {
            return ResendGenericMessage;
        }

        var cutoff = UtcNow().AddSeconds(-60);

        if (await _repository.HasVerificationTokenCreatedAfterAsync(
                user.Id,
                cutoff,
                cancellationToken))
        {
            return ResendGenericMessage;
        }

        var fullName = await _repository.GetFullNameAsync(
            user.Id,
            user.Role,
            cancellationToken);

        var now = UtcNow();

        // Vô hiệu hóa các mã cũ để chỉ mã mới nhất dùng được.
        foreach (var old in await _repository.GetUnusedVerificationTokensAsync(
                     user.Id,
                     cancellationToken))
        {
            old.UsedAt = now;
        }

        var code = GenerateVerificationCode();

        _repository.AddVerificationToken(new EmailVerificationToken
        {
            UserId = user.Id,
            TokenHash = HashCode(user.Email, code),
            ExpiresAt = now.AddMinutes(CodeLifetimeMinutes),
            CreatedAt = now
        });

        await _repository.SaveChangesAsync(cancellationToken);

        await TrySendVerificationEmailAsync(
            fullName ?? user.Email,
            user.Email,
            code);

        return ResendGenericMessage;
    }

    // ================= HÀM HỖ TRỢ =================

    private DateTime UtcNow() => _clock.GetUtcNow().UtcDateTime;

    // Mã 6 số sinh bằng CSPRNG, giữ cả số 0 ở đầu.
    private static string GenerateVerificationCode()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }

    // Hash gắn với email nên hai user trùng mã vẫn ra hash khác nhau.
    // Database chỉ lưu hash, không lưu mã gốc.
    private static string HashCode(string email, string code)
    {
        var input = $"{email.Trim().ToLowerInvariant()}:{code}";

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }

    // Non-UI function: GenerateParentLinkingCode. Định dạng PAR-2026-ABC123.
    private async Task<string> GenerateParentLinkingCodeAsync(
        CancellationToken cancellationToken)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var chars = new char[6];

            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            }

            var code = $"PAR-{_clock.GetUtcNow().Year}-{new string(chars)}";

            if (!await _repository.ParentLinkingCodeExistsAsync(code, cancellationToken))
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
        string code)
    {
        try
        {
            var body =
                $"<p>Xin chào {WebUtility.HtmlEncode(fullName)},</p>" +
                "<p>Mã xác thực email EduCenter của bạn là:</p>" +
                "<p style=\"font-size:32px;font-weight:700;" +
                $"letter-spacing:8px;margin:16px 0\">{code}</p>" +
                $"<p>Mã có hiệu lực trong {CodeLifetimeMinutes} phút. " +
                "Vui lòng không chia sẻ mã này cho bất kỳ ai.</p>" +
                "<p>Nếu bạn không đăng ký tài khoản này, hãy bỏ qua email.</p>";

            await _emailSender.SendAsync(
                toEmail,
                "Mã xác thực tài khoản EduCenter",
                body);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification email to {Email}", toEmail);
            return false;
        }
    }
}