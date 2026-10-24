using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.ViewModels.Common;
using EduCenterManagement.ViewModels.Notification;
using Microsoft.EntityFrameworkCore;
namespace EduCenterManagement.Repositories;
public sealed class NotificationRepository(ApplicationDbContext db) : INotificationRepository
{
 public async Task AddAsync(Notification n, CancellationToken ct=default){ db.Notifications.Add(n); await db.SaveChangesAsync(ct); }
 public Task<IReadOnlyList<int>> GetAcceptedParentUserIdsAsync(int studentId,CancellationToken ct=default)=>db.ParentStudentLinks.AsNoTracking().Where(x=>x.StudentId==studentId&&x.Status==ParentStudentLinkStatus.Accepted).Select(x=>x.Parent.UserId).ToListAsync(ct).ContinueWith(x=>(IReadOnlyList<int>)x.Result,ct);
 public Task<int> GetUnreadCountAsync(int userId,CancellationToken ct=default)=>db.Notifications.AsNoTracking().CountAsync(x=>x.UserId==userId&&!x.IsRead,ct);
 public async Task<PagedResult<NotificationItemViewModel>> GetListAsync(int userId,bool unreadOnly,int? studentId,int page,int pageSize,CancellationToken ct=default){ var q=db.Notifications.AsNoTracking().Where(x=>x.UserId==userId); if(unreadOnly)q=q.Where(x=>!x.IsRead);if(studentId.HasValue)q=q.Where(x=>x.StudentId==studentId);var total=await q.CountAsync(ct);var items=await q.OrderByDescending(x=>x.CreatedAt).Skip((page-1)*pageSize).Take(pageSize).Select(x=>new NotificationItemViewModel{Id=x.Id,Type=x.Type,Title=x.Title,Message=x.Message,ReferenceType=x.ReferenceType,ReferenceId=x.ReferenceId,StudentId=x.StudentId,IsRead=x.IsRead,CreatedAt=x.CreatedAt}).ToListAsync(ct);return new(){Items=items,Page=page,PageSize=pageSize,TotalCount=total}; }
 public async Task<bool> MarkAsReadAsync(int userId,int id,CancellationToken ct=default){var n=await db.Notifications.SingleOrDefaultAsync(x=>x.Id==id&&x.UserId==userId,ct);if(n is null)return false;n.IsRead=true;n.ReadAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);return true;}
 public async Task MarkAllAsReadAsync(int userId,CancellationToken ct=default){var list=await db.Notifications.Where(x=>x.UserId==userId&&!x.IsRead).ToListAsync(ct);foreach(var n in list){n.IsRead=true;n.ReadAt=DateTime.UtcNow;}await db.SaveChangesAsync(ct);}
}
