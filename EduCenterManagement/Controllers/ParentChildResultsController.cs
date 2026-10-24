using EduCenterManagement.Services;
using EduCenterManagement.Services.Common;
using Microsoft.AspNetCore.Mvc;

namespace EduCenterManagement.Controllers;

// TEMP: no authentication yet. Replace QueryStringCurrentUserContext with claims-based implementation and add [Authorize] when auth is merged.
[Route("ParentChildren/QuizResults")]
public sealed class ParentChildResultsController(
    ICurrentUserContext currentUserContext,
    IParentChildResultsService resultsService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> QuizResults(int studentId, int? classId, int page = 1, CancellationToken ct = default)
    {
        if (currentUserContext.UserId is null)
        {
            return View("~/Views/ParentChildren/NoUser.cshtml");
        }

        var results = await resultsService.GetQuizResultsAsync(studentId, classId, page, ct);
        if (results is null)
        {
            var parentProfileId = await currentUserContext.GetParentProfileIdAsync(ct);
            if (parentProfileId is null)
            {
                ViewData["ProfileMessage"] = "This user does not have a Parent profile.";
                return View("~/Views/ParentChildren/NoUser.cshtml");
            }

            return NotFound();
        }

        return View("~/Views/ParentChildren/QuizResults.cshtml", results);
    }
}
