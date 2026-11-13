using EduCenterManagement.Enums;
using EduCenterManagement.Services.Admin;
using EduCenterManagement.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduCenterManagement.Controllers.Admin;

[Authorize(Roles = "Admin")]
public class ClassesController : Controller
{
    private readonly IAdminClassService _service;
    public ClassesController(IAdminClassService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Index(int? subjectId = null, int? teacherId = null, string? status = null, int page = 1)
    {
        var result = await _service.GetIndexAsync(subjectId, teacherId, status, page);
        if (!result.Success) return BadRequest(result.Message);
        var data = result.Data!;
        SetFormData(data.Form);
        ViewBag.FilterSubjects = data.FilterSubjects;
        ViewBag.FilterTeachers = data.FilterTeachers;
        ViewBag.FilterStatuses = data.FilterStatuses;
        ViewBag.HasClassFilters = data.HasClassFilters;
        ViewBag.Pagination = data.Pagination;
        return View("~/Views/Admin/Classes/Index.cshtml", data.Classes);
    }

    [HttpGet]
    public IActionResult Create() => RedirectToAction(nameof(Index));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ClassViewModel model)
    {
        model.Name = (model.Name ?? string.Empty).Trim();
        model.Schedules ??= new List<ClassScheduleInputViewModel>();
        if (!ModelState.IsValid) return ReturnCreateError();
        var result = await _service.CreateAsync(model);
        if (!result.Success)
        {
            AddErrors(result);
            return ReturnCreateError();
        }
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CheckConflict(int teacherId, int? classId, DateOnly? startDate, DateOnly? endDate,
        int? timeSlotId, [FromQuery] int[] days, [FromQuery] int[] timeSlotIds)
        => DisplayJson(await _service.CheckConflictAsync(teacherId, classId, startDate, endDate, timeSlotId, days, timeSlotIds), useOk: true);

    [HttpGet]
    public async Task<IActionResult> AssignInfo(int id) => DisplayJson(await _service.GetAssignInfoAsync(id));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignTeacher(AssignTeacherViewModel model)
    {
        if (!ModelState.IsValid) return BadRequest(new { message = "Dữ liệu phân công không hợp lệ." });
        return DisplayJson(await _service.AssignTeacherAsync(model), useOk: true);
    }

    [HttpGet]
    public async Task<IActionResult> Capacity(int id) => DisplayJson(await _service.GetCapacityAsync(id));

    [HttpGet]
    public async Task<IActionResult> Details(int? id)
    {
        if (!id.HasValue) return NotFound();
        var data = await _service.GetDetailsAsync(id.Value);
        if (data == null) return NotFound();
        ViewBag.SessionCount = data.SessionCount;
        ViewBag.HasPaidEnrollments = data.HasPaidEnrollments;
        return View("~/Views/Admin/Classes/Details.cshtml", data.Class);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateSessions(int id)
    {
        var result = await _service.GenerateSessionsAsync(id);
        if (result.Status == AdminResultStatus.NotFound) return NotFound();
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (!id.HasValue) return NotFound();
        var result = await _service.GetEditAsync(id.Value);
        if (result.Status == AdminResultStatus.NotFound) return NotFound();
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
        SetFormData(result.Data!.Form);
        return View("~/Views/Admin/Classes/Edit.cshtml", result.Data.Model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ClassViewModel model)
    {
        if (id != model.Id) return NotFound();
        var result = await _service.EditAsync(id, model, ModelState.IsValid);
        if (result.Status == AdminResultStatus.NotFound) return NotFound();
        if (result.Status == AdminResultStatus.Conflict) return Conflict(result.Message);
        if (result.Status == AdminResultStatus.BadRequest) return BadRequest(result.Message);
        SetFormData(result.Data?.Form);
        if (!result.Success)
        {
            AddErrors(result);
            return View("~/Views/Admin/Classes/Edit.cshtml", model);
        }
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int? id)
    {
        if (!id.HasValue) return NotFound();
        var result = await _service.GetDeleteAsync(id.Value);
        if (result.Status == AdminResultStatus.NotFound) return NotFound();
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
        return View("~/Views/Admin/Classes/Delete.cshtml", result.Data);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteConfirmed(int id) => ChangeStatus(id, ClassStatus.Cancelled);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, ClassStatus targetStatus)
    {
        if (!ModelState.IsValid) return BadRequest("Trạng thái không hợp lệ.");
        var result = await _service.ChangeStatusAsync(id, targetStatus);
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> GetTeachersBySubject(int subjectId) => Json(await _service.GetTeachersBySubjectAsync(subjectId));

    private IActionResult DisplayJson(AdminResult<object> result, bool useOk = false)
    {
        if (result.Status == AdminResultStatus.NotFound) return NotFound();
        if (result.Status == AdminResultStatus.BadRequest) return BadRequest(result.Data);
        if (result.Status == AdminResultStatus.Conflict) return Conflict(result.Data);
        return useOk ? Ok(result.Data) : Json(result.Data);
    }
    private void SetFormData(AdminClassFormData? form)
    {
        if (form == null) return;
        ViewBag.Subjects = form.Subjects;
        ViewBag.Teachers = form.Teachers;
        ViewBag.TimeSlots = form.TimeSlots;
    }
    private void AddErrors(AdminResult result)
    {
        foreach (var error in result.Errors) ModelState.AddModelError(error.Field, error.Message);
    }
    private IActionResult ReturnCreateError()
    {
        var message = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
            .Where(e => !string.IsNullOrWhiteSpace(e)));
        TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(message) ? "Dữ liệu lớp học không hợp lệ." : message;
        return RedirectToAction(nameof(Index));
    }
}
