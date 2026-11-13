using EduCenterManagement.Services.Admin;
using EduCenterManagement.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduCenterManagement.Controllers.Admin;

[Authorize(Roles = "Admin")]
public class SubjectsController : Controller
{
    private readonly IAdminSubjectService _service;
    public SubjectsController(IAdminSubjectService service) => _service = service;

    public async Task<IActionResult> Index(int page = 1)
    {
        var data = await _service.GetIndexAsync(page);
        ViewBag.Pagination = data.Pagination;
        return View("~/Views/Admin/Subjects/Index.cshtml", data.Subjects);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var subject = await _service.GetByIdAsync(id.Value);
        return subject == null ? NotFound() : View("~/Views/Admin/Subjects/Details.cshtml", subject);
    }

    [HttpGet]
    public IActionResult Create() => View("~/Views/Admin/Subjects/Create.cshtml", new SubjectViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SubjectViewModel model)
    {
        if (!ModelState.IsValid) return View("~/Views/Admin/Subjects/Create.cshtml", model);
        var result = await _service.CreateAsync(model);
        if (!result.Success)
        {
            AddErrors(result);
            return View("~/Views/Admin/Subjects/Create.cshtml", model);
        }
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var model = await _service.GetEditAsync(id.Value);
        return model == null ? NotFound() : View("~/Views/Admin/Subjects/Edit.cshtml", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SubjectViewModel model)
    {
        if (id != model.Id) return NotFound();
        if (!ModelState.IsValid) return View("~/Views/Admin/Subjects/Edit.cshtml", model);
        var result = await _service.EditAsync(id, model);
        if (result.Status == AdminResultStatus.NotFound) return NotFound();
        if (!result.Success)
        {
            AddErrors(result);
            return View("~/Views/Admin/Subjects/Edit.cshtml", model);
        }
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var subject = await _service.GetByIdAsync(id.Value);
        return subject == null ? NotFound() : View("~/Views/Admin/Subjects/Delete.cshtml", subject);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var result = await _service.DeactivateAsync(id);
        if (result.Status == AdminResultStatus.NotFound) return NotFound();
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private void AddErrors(AdminResult result)
    {
        foreach (var error in result.Errors) ModelState.AddModelError(error.Field, error.Message);
    }
}
