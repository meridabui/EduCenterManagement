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
            var model = await BuildDashboardViewModelAsync();
            return model == null ? Forbid() : View(model);
        }

        [HttpGet]
        public async Task<IActionResult> AcademicTracking()
        {
            var model = await BuildDashboardViewModelAsync();
            return model == null ? Forbid() : View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Links()
        {
            var model = await BuildDashboardViewModelAsync();
            return model == null ? Forbid() : View(model);
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
                return RedirectToAction(nameof(Links));
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
                return RedirectToAction(nameof(Links));
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
                return RedirectToAction(nameof(Links));
            }

            TempData["Success"] = accept
                ? $"Đã chấp nhận liên kết với học sinh {link.Student.FullName}."
                : $"Đã từ chối yêu cầu của học sinh {link.Student.FullName}.";

            return RedirectToAction(nameof(Links));
        }

        private async Task<ParentDashboardViewModel?> BuildDashboardViewModelAsync()
        {
            var parent = await GetCurrentParentAsync();
            if (parent == null)
                return null;

            var links = await _context.ParentStudentLinks
                .AsNoTracking()
                .Where(link => link.ParentId == parent.Id &&
                    (link.Status == ParentStudentLinkStatus.Pending ||
                     link.Status == ParentStudentLinkStatus.Accepted))
                .OrderByDescending(link => link.RequestedAt)
                .Select(link => new LinkItemViewModel
                {
                    LinkId = link.Id,
                    StudentId = link.StudentId,
                    FullName = link.Student.FullName,
                    Email = link.Student.User.Email,
                    Status = link.Status,
                    RequestedAt = link.RequestedAt,
                    RespondedAt = link.RespondedAt
                })
                .ToListAsync();

            var notifications = await _context.Notifications
                .AsNoTracking()
                .Where(notification => notification.UserId == parent.UserId)
                .OrderByDescending(notification => notification.CreatedAt)
                .Take(5)
                .Select(notification => new NotificationItemViewModel
                {
                    Title = notification.Title,
                    Message = notification.Message,
                    CreatedAt = notification.CreatedAt
                })
                .ToListAsync();

            return new ParentDashboardViewModel
            {
                FullName = parent.FullName,
                LinkingCode = parent.LinkingCode,
                PendingRequests = links
                    .Where(link => link.Status == ParentStudentLinkStatus.Pending)
                    .ToList(),
                LinkedStudents = links
                    .Where(link => link.Status == ParentStudentLinkStatus.Accepted)
                    .ToList(),
                Notifications = notifications
            };
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
