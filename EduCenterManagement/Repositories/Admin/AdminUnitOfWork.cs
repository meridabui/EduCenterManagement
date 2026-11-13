using System.Data;
using EduCenterManagement.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EduCenterManagement.Repositories.Admin;

public class AdminUnitOfWork : IAdminUnitOfWork
{
    private readonly ApplicationDbContext _context;
    public AdminUnitOfWork(ApplicationDbContext context) => _context = context;
    public Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        => _context.Database.BeginTransactionAsync(isolationLevel);
    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
}
