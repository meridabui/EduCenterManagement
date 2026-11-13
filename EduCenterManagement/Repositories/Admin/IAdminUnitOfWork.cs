using System.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace EduCenterManagement.Repositories.Admin;

// All Admin repositories share the scoped ApplicationDbContext and its transaction.
public interface IAdminUnitOfWork
{
    Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);
    Task<int> SaveChangesAsync();
}
