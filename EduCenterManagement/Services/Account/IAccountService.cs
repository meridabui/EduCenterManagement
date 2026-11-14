using EduCenterManagement.Models;
using EduCenterManagement.ViewModels.Account;

namespace EduCenterManagement.Services.Account;

public enum LoginStatus
{
    Success,
    InvalidCredentials,
    PendingVerification
}

public sealed record LoginResult(LoginStatus Status, AppUser? User = null);

public enum RegisterStatus
{
    Success,
    InvalidRole,
    EmailTaken,
    SaveFailed
}

public sealed record RegisterResult(
    RegisterStatus Status,
    string Message,
    bool EmailSent = false);

public sealed record EmailVerificationResult(bool Success, string Message);

public interface IAccountService
{
    // BR01: kiểm tra thông tin đăng nhập và trạng thái tài khoản.
    Task<LoginResult> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    // BR01, BR02: tạo tài khoản Parent/Student chờ xác thực email.
    // buildVerificationLink nhận raw token và trả về URL tuyệt đối.
    Task<RegisterResult> RegisterAsync(
        RegisterViewModel model,
        CancellationToken cancellationToken = default);

    // BR03: xác thực email và sinh Parent Linking Code.
    Task<EmailVerificationResult> VerifyEmailAsync(
        string? email,
        string? code,
        CancellationToken cancellationToken = default);

    // Luôn trả về thông báo chung để không lộ email có tồn tại hay không.
    Task<string> ResendVerificationAsync(
        string email,
        CancellationToken cancellationToken = default);
}
