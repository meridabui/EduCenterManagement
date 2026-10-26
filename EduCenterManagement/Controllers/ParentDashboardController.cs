using System.Security.Claims;
using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Controllers
{
    [Authorize(Roles = "Parent")]
    public class ParentDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ParentDashboardController> _logger;

        public ParentDashboardController(
            ApplicationDbContext context,
            ILogger<ParentDashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /ParentDashboard
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var parent = await GetCurrentParentAsync();

            if (parent == null)
            {
                return Forbid();
            }

            var links = await _context.ParentStudentLinks
                .AsNoTracking()
                .Where(l => l.ParentId == parent.Id &&
                            (l.Status == ParentStudentLinkStatus.Pending ||
                             l.Status == ParentStudentLinkStatus.Accepted))
                .OrderByDescending(l => l.RequestedAt)
                .Select(l => new LinkItemViewModel
                {
                    LinkId = l.Id,
                    FullName = l.Student.FullName,
                    Email = l.Student.User.Email,
                    Status = l.Status,
                    RequestedAt = l.RequestedAt,
                    RespondedAt = l.RespondedAt
                })
                .ToListAsync();

            var notifications = await _context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == parent.UserId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .Select(n => new NotificationItemViewModel
                {
                    Title = n.Title,
                    Message = n.Message,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();

            return View(new ParentDashboardViewModel
            {
                FullName = parent.FullName,
                LinkingCode = parent.LinkingCode,
                PendingRequests = links
                    .Where(l => l.Status == ParentStudentLinkStatus.Pending)
                    .ToList(),
                LinkedStudents = links
                    .Where(l => l.Status == ParentStudentLinkStatus.Accepted)
                    .ToList(),
                Notifications = notifications
            });
        }

        // POST: /ParentDashboard/Respond
        // decision = "accept" hoặc "reject" (state machine 7.3).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Respond(int id, string? decision)
        {
            var parent = await GetCurrentParentAsync();

            if (parent == null)
            {
                return Forbid();
            }

            var accept = string.Equals(decision, "accept", StringComparison.OrdinalIgnoreCase);
            var reject = string.Equals(decision, "reject", StringComparison.OrdinalIgnoreCase);

            if (!accept && !reject)
            {
                TempData["Error"] = "Thao tác không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            // Chỉ xử lý yêu cầu gửi tới chính Parent này.
            var link = await _context.ParentStudentLinks
                .Include(l => l.Student)
                .SingleOrDefaultAsync(l => l.Id == id && l.ParentId == parent.Id);

            if (link == null)
            {
                return NotFound();
            }

            if (link.Status != ParentStudentLinkStatus.Pending)
            {
                TempData["Error"] = "Yêu cầu này đã được xử lý trước đó.";
                return RedirectToAction(nameof(Index));
            }

            var now = DateTime.UtcNow;

            link.Status = accept
                ? ParentStudentLinkStatus.Accepted
                : ParentStudentLinkStatus.Rejected;
            link.RespondedAt = now;

            _context.Notifications.Add(new Notification
            {
                UserId = link.Student.UserId,
                Type = "LinkResponse",
                Title = accept ? "Yêu cầu liên kết được chấp nhận" : "Yêu cầu liên kết bị từ chối",
                Message = accept
                    ? $"Phụ huynh {parent.FullName} đã chấp nhận liên kết với bạn."
                    : $"Phụ huynh {parent.FullName} đã từ chối yêu cầu liên kết của bạn.",
                ReferenceType = "ParentStudentLink",
                ReferenceId = link.Id,
                CreatedAt = now
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Respond failed for link {LinkId}", id);
                TempData["Error"] = "Không thể xử lý yêu cầu. Vui lòng thử lại.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = accept
                ? $"Đã chấp nhận liên kết với học sinh {link.Student.FullName}."
                : $"Đã từ chối yêu cầu của học sinh {link.Student.FullName}.";

            return RedirectToAction(nameof(Index));
        }

        private async Task<ParentProfile?> GetCurrentParentAsync()
        {
            var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdText, out var userId))
            {
                return null;
            }

            return await _context.ParentProfiles
                .AsNoTracking()
                .SingleOrDefaultAsync(p => p.UserId == userId);
        }
    }
}
