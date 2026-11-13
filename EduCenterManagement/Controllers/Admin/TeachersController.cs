using EduCenterManagement.Services.Admin;
using EduCenterManagement.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduCenterManagement.Controllers.Admin;

[Authorize(Roles = "Admin")]
public class TeachersController : Controller
{
    private readonly IAdminTeacherService _service;
    public TeachersController(IAdminTeacherService service) => _service = service;

    public async Task<IActionResult> Index(int page = 1, int? subjectId = null)
    {
        var data = await _service.GetIndexAsync(page, subjectId);
        ViewBag.Subjects = new SelectList(data.Subjects, "Id", "Name", subjectId);
        ViewBag.SelectedSubjectId = subjectId;
        ViewBag.Pagination = data.Pagination;
        return View("~/Views/Admin/Teachers/Index.cshtml", data.Teachers);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Subjects = await _service.GetSubjectsAsync();
        return View("~/Views/Admin/Teachers/Create.cshtml", new CreateTeacherViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTeacherViewModel model)
    {
        model.SubjectIds = (model.SubjectIds ?? new List<int>()).Distinct().ToList();
        ViewBag.Subjects = await _service.GetSubjectsAsync(model.SubjectIds);
        if (!ModelState.IsValid) return View("~/Views/Admin/Teachers/Create.cshtml", model);
        var result = await _service.CreateAsync(model);
        if (!result.Success)
        {
            AddErrors(result);
            return View("~/Views/Admin/Teachers/Create.cshtml", model);
        }
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> AssignSubjects(int id)
    {
        var data = await _service.GetAssignSubjectsAsync(id);
        if (data == null) return NotFound();
        ViewBag.TeacherName = data.TeacherName;
        ViewBag.Subjects = data.Subjects;
        return View("~/Views/Admin/Teachers/AssignSubjects.cshtml", data.Model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignSubjects(AssignTeacherSubjectsViewModel model)
    {
        var result = await _service.AssignSubjectsAsync(model, ModelState.IsValid);
        if (result.Status == AdminResultStatus.NotFound) return NotFound();
        ViewBag.TeacherName = result.Data!.TeacherName;
        ViewBag.Subjects = result.Data.Subjects;
        if (!result.Success)
        {
            AddErrors(result);
            return View("~/Views/Admin/Teachers/AssignSubjects.cshtml", model);
        }
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private void AddErrors(AdminResult result)
    {
        foreach (var error in result.Errors) ModelState.AddModelError(error.Field, error.Message);
    }
}
