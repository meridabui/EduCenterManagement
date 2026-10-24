// TEMP: remove before merge to main
using EduCenterManagement.Data;
using Microsoft.AspNetCore.Mvc;

namespace EduCenterManagement.DevSupport;

[Route("Dev")]
public sealed class DevSeedExtrasController(ApplicationDbContext db, IWebHostEnvironment environment) : Controller
{
    [HttpGet("SeedMore")]
    public async Task<IActionResult> SeedMore(CancellationToken ct)
    {
        if (!environment.IsDevelopment()) return NotFound();
        await DevSeedExtras.SeedAsync(db, ct);
        return RedirectToAction("Index", "Dev");
    }
}
