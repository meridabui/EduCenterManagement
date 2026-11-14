using EduCenterManagement.Enums;
using EduCenterManagement.Models;

namespace EduCenterManagement.Repositories.Account;

public interface IAccountRepository
{
    Task<AppUser?> GetUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    // Tài khoản đang chờ xác thực email (dùng cho gửi lại email xác thực).
    Task<AppUser?> GetPendingUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken = default);

    // Trả về token kèm User (tracked) để cập nhật trạng thái khi xác thực.y
    Task<List<EmailVerificationToken>> GetUnusedVerificationTokensAsync(
        int userId,
        CancellationToken cancellationToken = default);
    Task<ParentProfile?> GetParentProfileByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<bool> ParentLinkingCodeExistsAsync(
        string linkingCode,
        CancellationToken cancellationToken = default);

    Task<bool> HasVerificationTokenCreatedAfterAsync(
        int userId,
        DateTime since,
        CancellationToken cancellationToken = default);

    Task<string?> GetFullNameAsync(
        int userId,
        UserRole role,
        CancellationToken cancellationToken = default);

    void AddParentProfile(ParentProfile profile);

    void AddStudentProfile(StudentProfile profile);

    void AddVerificationToken(EmailVerificationToken token);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
