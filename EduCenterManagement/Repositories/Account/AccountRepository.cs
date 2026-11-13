using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Repositories.Account;

public sealed class AccountRepository : IAccountRepository
{
    private readonly ApplicationDbContext _context;

    public AccountRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<AppUser?> GetUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        return _context.Users
            .SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public Task<AppUser?> GetPendingUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        return _context.Users.SingleOrDefaultAsync(
            u => u.Email == email &&
                 u.Status == UserStatus.PendingVerification,
            cancellationToken);
    }

    public Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        return _context.Users.AnyAsync(u => u.Email == email, cancellationToken);
    }

    public Task<List<EmailVerificationToken>> GetUnusedVerificationTokensAsync(
      int userId,
      CancellationToken cancellationToken = default)
    {
        return _context.EmailVerificationTokens
            .Where(t => t.UserId == userId && t.UsedAt == null)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<ParentProfile?> GetParentProfileByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        return _context.ParentProfiles
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
    }

    public Task<bool> ParentLinkingCodeExistsAsync(
        string linkingCode,
        CancellationToken cancellationToken = default)
    {
        return _context.ParentProfiles
            .AnyAsync(p => p.LinkingCode == linkingCode, cancellationToken);
    }

    public Task<bool> HasVerificationTokenCreatedAfterAsync(
        int userId,
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        return _context.EmailVerificationTokens.AnyAsync(
            t => t.UserId == userId && t.CreatedAt > since,
            cancellationToken);
    }

    public Task<string?> GetFullNameAsync(
        int userId,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        return role == UserRole.Parent
            ? _context.ParentProfiles
                .Where(p => p.UserId == userId)
                .Select(p => p.FullName)
                .FirstOrDefaultAsync(cancellationToken)
            : _context.StudentProfiles
                .Where(s => s.UserId == userId)
                .Select(s => s.FullName)
                .FirstOrDefaultAsync(cancellationToken);
    }

    public void AddParentProfile(ParentProfile profile)
    {
        _context.ParentProfiles.Add(profile);
    }

    public void AddStudentProfile(StudentProfile profile)
    {
        _context.StudentProfiles.Add(profile);
    }

    public void AddVerificationToken(EmailVerificationToken token)
    {
        _context.EmailVerificationTokens.Add(token);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
