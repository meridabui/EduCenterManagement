using EduCenterManagement.Models;
using EduCenterManagement.ViewModels.Common;
using EduCenterManagement.ViewModels.Notification;
namespace EduCenterManagement.Repositories;
public interface INotificationRepository { Task AddAsync(Notification notification, CancellationToken ct = default); Task<IReadOnlyList<int>> GetAcceptedParentUserIdsAsync(int studentId, CancellationToken ct = default); Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default); Task<PagedResult<NotificationItemViewModel>> GetListAsync(int userId, bool unreadOnly, int? studentId, int page, int pageSize, CancellationToken ct = default); Task<bool> MarkAsReadAsync(int userId, int notificationId, CancellationToken ct = default); Task MarkAllAsReadAsync(int userId, CancellationToken ct = default); }
