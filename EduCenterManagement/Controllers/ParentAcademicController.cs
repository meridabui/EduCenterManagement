using System.Security.Claims;
using EduCenterManagement.Services.Parent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduCenterManagement.Controllers;

[Authorize(Roles = "Parent")]
public sealed class ParentAcademicController : Controller
{
    private readonly IParentAcademicService _service;

    public ParentAcademicController(IParentAcademicService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Comments(
        int studentId,
        int? classId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Forbid();

        var model = await _service.GetCommentsAsync(
            userId.Value, studentId, classId, cancellationToken);
        return model == null ? NotFound() : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Attendance(
        int studentId,
        int? classId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Forbid();

        var model = await _service.GetAttendanceAsync(
            userId.Value, studentId, classId, cancellationToken);
        return model == null ? NotFound() : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Timetable(
        int studentId,
        int? classId,
        DateOnly? weekStart,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Forbid();

        var model = await _service.GetTimetableAsync(
            userId.Value, studentId, classId, weekStart, cancellationToken);
        return model == null ? NotFound() : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> QuizResults(
        int studentId,
        int? classId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Forbid();

        var model = await _service.GetQuizResultsAsync(
            userId.Value, studentId, classId, cancellationToken);
        return model == null ? NotFound() : View(model);
    }

    private int? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var userId) ? userId : null;
    }
}
