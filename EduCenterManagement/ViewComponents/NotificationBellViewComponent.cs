using EduCenterManagement.Services;
using EduCenterManagement.Services.Common;
using Microsoft.AspNetCore.Mvc;
namespace EduCenterManagement.ViewComponents;
public sealed class NotificationBellViewComponent(ICurrentUserContext current, INotificationService notifications):ViewComponent { public async Task<IViewComponentResult> InvokeAsync(){ if(current.UserId is not int id)return Content(string.Empty);return View(await notifications.GetUnreadCountAsync(id,HttpContext.RequestAborted));} }
