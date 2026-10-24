using EduCenterManagement.ViewModels.Common;
using EduCenterManagement.ViewModels.Notification;
namespace EduCenterManagement.Services;
public interface INotificationService
{
    /// <summary>Creates and persists a notification immediately.</summary>
    Task CreateAsync(int userId,string type,string title,string message,string? referenceType=null,int? referenceId=null,int? studentId=null,CancellationToken ct=default);
    Task CreateForParentsOfStudentAsync(int studentProfileId,string type,string title,string message,string? referenceType=null,int? referenceId=null,CancellationToken ct=default);
    Task<int> GetUnreadCountAsync(int userId,CancellationToken ct=default);
    Task<PagedResult<NotificationItemViewModel>> GetListAsync(int userId,bool unreadOnly,int? studentId,int page,int pageSize,CancellationToken ct=default);
    Task MarkAsReadAsync(int userId,int notificationId,CancellationToken ct=default);
    Task MarkAllAsReadAsync(int userId,CancellationToken ct=default);
}
