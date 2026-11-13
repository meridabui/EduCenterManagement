using EduCenterManagement.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduCenterManagement.Controllers.Admin;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly IAdminDashboardService _service;
    public AdminController(IAdminDashboardService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Index()
        => View("~/Views/Admin/Index.cshtml", await _service.GetDashboardAsync());
}
