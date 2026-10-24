using EduCenterManagement.Services; using EduCenterManagement.Services.Common; using Microsoft.AspNetCore.Mvc;
namespace EduCenterManagement.Controllers;
// TEMP: no authentication yet. Replace QueryStringCurrentUserContext with claims-based implementation and add [Authorize] when auth is merged.
[Route("ParentChildren/Comments")]
public sealed class ParentChildCommentsController(ICurrentUserContext current,IParentChildCommentsService service):Controller { [HttpGet] public async Task<IActionResult> Comments(int studentId,int? classId,int page=1,CancellationToken ct=default){if(current.UserId is null)return View("~/Views/ParentChildren/NoUser.cshtml");var m=await service.GetCommentsAsync(studentId,classId,page,ct);if(m is not null)return View("~/Views/ParentChildren/Comments.cshtml",m);if(await current.GetParentProfileIdAsync(ct) is null){ViewData["ProfileMessage"]="This user does not have a Parent profile.";return View("~/Views/ParentChildren/NoUser.cshtml");}return NotFound();} }
