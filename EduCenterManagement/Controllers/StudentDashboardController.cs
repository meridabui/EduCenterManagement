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
    [Authorize(Roles = "Student")]
    public class StudentDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<StudentDashboardController> _logger;

        public StudentDashboardController(
            ApplicationDbContext context,
            ILogger<StudentDashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /StudentDashboard
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var student = await GetCurrentStudentAsync();

            if (student == null)
            {
                return Forbid();
            }

            var links = await _context.ParentStudentLinks
                .AsNoTracking()
                .Where(l => l.StudentId == student.Id)
                .OrderByDescending(l => l.RequestedAt)
                .Select(l => new LinkItemViewModel
                {
                    LinkId = l.Id,
                    FullName = l.Parent.FullName,
                    Email = l.Parent.User.Email,
                    Status = l.Status,
                    RequestedAt = l.RequestedAt,
                    RespondedAt = l.RespondedAt
                })
                .ToListAsync();

            var notifications = await _context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == student.UserId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .Select(n => new NotificationItemViewModel
                {
                    Title = n.Title,
                    Message = n.Message,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();

            return View(new StudentDashboardViewModel
            {
                FullName = student.FullName,
                Links = links,
                Notifications = notifications
            });
        }

        // POST: /StudentDashboard/SendLinkRequest
        // BR04: Student chỉ gửi được yêu cầu khi nhập đúng Parent Linking Code.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendLinkRequest(string? linkingCode)
        {
            var student = await GetCurrentStudentAsync();

            if (student == null)
            {
                return Forbid();
            }

            var code = linkingCode?.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(code))
            {
                TempData["Error"] = "Vui lòng nhập Parent Linking Code.";
                return RedirectToAction(nameof(Index));
            }

            // Chỉ Parent đã xác thực email (Active) mới có mã hợp lệ.
            var parent = await _context.ParentProfiles
                .SingleOrDefaultAsync(p =>
                    p.LinkingCode == code &&
                    p.User.Status == UserStatus.Active);

            if (parent == null)
            {
                TempData["Error"] = "Mã liên kết không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            var now = DateTime.UtcNow;

            var link = await _context.ParentStudentLinks
                .SingleOrDefaultAsync(l =>
                    l.ParentId == parent.Id &&
                    l.StudentId == student.Id);

            if (link != null)
            {
                switch (link.Status)
                {
                    case ParentStudentLinkStatus.Accepted:
                        TempData["Error"] = "Bạn đã được liên kết với phụ huynh này.";
                        return RedirectToAction(nameof(Index));

                    case ParentStudentLinkStatus.Pending:
                        TempData["Error"] = "Yêu cầu liên kết đang chờ phụ huynh phản hồi.";
                        return RedirectToAction(nameof(Index));
                }

                // BR06: yêu cầu bị từ chối (hoặc đã thu hồi) có thể gửi lại.
                link.Status = ParentStudentLinkStatus.Pending;
                link.RequestedAt = now;
                link.RespondedAt = null;
            }
            else
            {
                link = new ParentStudentLink
                {
                    ParentId = parent.Id,
                    StudentId = student.Id,
                    Status = ParentStudentLinkStatus.Pending,
                    RequestedAt = now
                };

                _context.ParentStudentLinks.Add(link);
            }

            try
            {
                await _context.SaveChangesAsync();

                _context.Notifications.Add(new Notification
                {
                    UserId = parent.UserId,
                    Type = "LinkRequest",
                    Title = "Yêu cầu liên kết mới",
                    Message = $"Học sinh {student.FullName} muốn liên kết với tài khoản của bạn.",
                    ReferenceType = "ParentStudentLink",
                    ReferenceId = link.Id,
                    CreatedAt = now
                });

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "SendLinkRequest failed for student {StudentId}", student.Id);
                TempData["Error"] = "Không thể gửi yêu cầu. Vui lòng thử lại.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = $"Đã gửi yêu cầu liên kết tới phụ huynh {parent.FullName}. " +
                                  "Vui lòng chờ phụ huynh chấp nhận.";

            return RedirectToAction(nameof(Index));
        }

        private async Task<StudentProfile?> GetCurrentStudentAsync()
        {
            var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdText, out var userId))
            {
                return null;
            }

            return await _context.StudentProfiles
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.UserId == userId);
        }
    }
}
