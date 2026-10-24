using EduCenterManagement.Services;
using EduCenterManagement.Services.Common;
using Microsoft.AspNetCore.Mvc;
namespace EduCenterManagement.Controllers;
// TEMP: no authentication yet. Replace QueryStringCurrentUserContext with claims-based implementation and add [Authorize] when auth is merged.
public sealed class NotificationController(ICurrentUserContext current,INotificationService service):Controller
{ public async Task<IActionResult> Index(bool unreadOnly=false,int? studentId=null,int page=1,CancellationToken ct=default){if(current.UserId is not int id)return View("NoUser");return View(await service.GetListAsync(id,unreadOnly,studentId,Math.Max(1,page),10,ct));} [HttpPost][ValidateAntiForgeryToken] public async Task<IActionResult> Read(int id,CancellationToken ct=default){if(current.UserId is not int uid)return View("NoUser");await service.MarkAsReadAsync(uid,id,ct);return RedirectToAction(nameof(Index));} [HttpPost][ValidateAntiForgeryToken] public async Task<IActionResult> MarkAll(CancellationToken ct=default){if(current.UserId is int id)await service.MarkAllAsReadAsync(id,ct);return RedirectToAction(nameof(Index));} }
