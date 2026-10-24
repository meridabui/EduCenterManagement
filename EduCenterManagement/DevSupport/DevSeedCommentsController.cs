// TEMP: remove before merge to main
using EduCenterManagement.Data;using Microsoft.AspNetCore.Mvc;
namespace EduCenterManagement.DevSupport;
[Route("Dev")]public sealed class DevSeedCommentsController(ApplicationDbContext db,IWebHostEnvironment environment):Controller{[HttpGet("SeedComments")]public async Task<IActionResult> SeedComments(CancellationToken ct){if(!environment.IsDevelopment())return NotFound();await DevSeedComments.SeedAsync(db,ct);return RedirectToAction("Index","Dev");}}
