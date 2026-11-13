using EduCenterManagement.Services.Admin;
using EduCenterManagement.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EduCenterManagement.Controllers.Admin;

[Authorize(Roles = "Admin")]
public class TimeSlotsController : Controller
{
    private readonly IAdminTimeSlotService _service;
    public TimeSlotsController(IAdminTimeSlotService service) => _service = service;

    public async Task<IActionResult> Index() => View("~/Views/Admin/TimeSlots/Index.cshtml", await _service.GetAllAsync());

    [HttpGet]
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var slot = await _service.GetByIdAsync(id.Value);
        return slot == null ? NotFound() : View("~/Views/Admin/TimeSlots/Details.cshtml", slot);
    }

    [HttpGet]
    public IActionResult Create() => View("~/Views/Admin/TimeSlots/Create.cshtml", new TimeSlotViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TimeSlotViewModel model)
    {
        var result = await _service.CreateAsync(model, ModelState.IsValid);
        if (!result.Success)
        {
            AddErrors(result);
            return View("~/Views/Admin/TimeSlots/Create.cshtml", model);
        }
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var model = await _service.GetEditAsync(id.Value);
        return model == null ? NotFound() : View("~/Views/Admin/TimeSlots/Edit.cshtml", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TimeSlotViewModel model)
    {
        if (id != model.Id) return NotFound();
        var validWithoutTimes = ModelState.Where(p => p.Key != nameof(model.StartTime) && p.Key != nameof(model.EndTime))
            .All(p => p.Value!.ValidationState is ModelValidationState.Valid or ModelValidationState.Skipped);
        var result = await _service.EditAsync(id, model, ModelState.IsValid, validWithoutTimes);
        if (result.Status == AdminResultStatus.NotFound) return NotFound();
        if (model.IsTimeLocked)
        {
            ModelState.Remove(nameof(model.StartTime));
            ModelState.Remove(nameof(model.EndTime));
        }
        if (!result.Success)
        {
            AddErrors(result);
            return View("~/Views/Admin/TimeSlots/Edit.cshtml", model);
        }
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var slot = await _service.GetByIdAsync(id.Value);
        return slot == null ? NotFound() : View("~/Views/Admin/TimeSlots/Delete.cshtml", slot);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id) => DisplayStatus(await _service.DeactivateAsync(id));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(int id) => DisplayStatus(await _service.ReactivateAsync(id));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateDefaultSlots() => DisplayStatus(await _service.GenerateDefaultSlotsAsync());

    private IActionResult DisplayStatus(AdminResult result)
    {
        if (result.Status == AdminResultStatus.NotFound) return NotFound();
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
    private void AddErrors(AdminResult result)
    {
        foreach (var error in result.Errors) ModelState.AddModelError(error.Field, error.Message);
    }
}
