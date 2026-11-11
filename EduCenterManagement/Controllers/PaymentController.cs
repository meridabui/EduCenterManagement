using System.Net;
using System.Security.Claims;
using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.Services;
using EduCenterManagement.Services.Email;
using EduCenterManagement.ViewModels.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Controllers
{
    [Authorize(Roles = "Parent")]
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPaymentService _paymentService;
        private readonly IVnPayService _vnPay;
        private readonly IAppEmailSender _emailSender;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            ApplicationDbContext context,
            IPaymentService paymentService,
            IVnPayService vnPay,
            IAppEmailSender emailSender,
            ILogger<PaymentController> logger)
        {
            _context = context;
            _paymentService = paymentService;
            _vnPay = vnPay;
            _emailSender = emailSender;
            _logger = logger;
        }

        // POST: /Payment/Checkout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(int enrollmentId)
        {
            var parent = await GetCurrentParentAsync();
            if (parent == null) return Forbid();

            PaymentResult result;
            try
            {
                result = await _paymentService.StartCheckoutAsync(parent.Id, enrollmentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Checkout crashed.");
                TempData["Error"] = "Có lỗi hệ thống khi thanh toán. Vui lòng thử lại.";
                return RedirectToAction("Confirmation", "Enrollment", new { id = enrollmentId });
            }

            if (!result.Success)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction("Confirmation", "Enrollment", new { id = enrollmentId });
            }

            return RedirectToAction(nameof(Gateway), new { id = result.PaymentId });
        }

        // GET: /Payment/Gateway/5  -> chuyển khách sang cổng VNPay
        [HttpGet]
        public async Task<IActionResult> Gateway(int id)
        {
            var parent = await GetCurrentParentAsync();
            if (parent == null) return Forbid();

            var payment = await _context.Payments
                .AsNoTracking()
                .Where(p => p.Id == id && p.Enrollment.RegisteredByParentId == parent.Id)
                .Select(p => new
                {
                    p.Id,
                    p.PaymentReference,
                    p.Amount,
                    p.Status,
                    p.EnrollmentId,
                    EnrollmentStatus = p.Enrollment.Status,
                    p.Enrollment.ReservedUntil
                })
                .SingleOrDefaultAsync();

            if (payment == null) return NotFound();

            var isActive =
                payment.Status == PaymentStatus.Pending &&
                payment.EnrollmentStatus == EnrollmentStatus.PendingPayment &&
                payment.ReservedUntil > DateTime.UtcNow;

            if (!isActive)
            {
                return RedirectToAction("Confirmation", "Enrollment", new { id = payment.EnrollmentId });
            }

            string paymentUrl;
            try
            {
                paymentUrl = _vnPay.CreatePaymentUrl(new VnPayPaymentRequest
                {
                    TxnRef = payment.PaymentReference ?? string.Empty,
                    Amount = payment.Amount,
                    OrderInfo = $"Thanh toan dang ky lop {payment.EnrollmentId}",
                    ReturnUrl = Url.Action(nameof(VnPayReturn), "Payment", null, Request.Scheme)!,
                    IpAddress = GetClientIp(),
                    ExpireAtUtc = DateTime.SpecifyKind(payment.ReservedUntil!.Value, DateTimeKind.Utc)
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "VNPay is not configured.");
                TempData["Error"] = "Cổng thanh toán chưa được cấu hình. Vui lòng liên hệ quản trị viên.";
                return RedirectToAction("Confirmation", "Enrollment", new { id = payment.EnrollmentId });
            }

            return Redirect(paymentUrl);
        }

        // GET: /Payment/VnPayReturn?vnp_...  (VNPay chuyển khách về đây)
        [HttpGet]
        public async Task<IActionResult> VnPayReturn()
        {
            var parent = await GetCurrentParentAsync();
            if (parent == null) return Forbid();

            var info = _vnPay.ParseReturn(Request.Query);

            if (!info.IsValidSignature)
            {
                _logger.LogWarning("VNPay return with invalid signature.");
                TempData["Error"] = "Không xác thực được kết quả từ VNPay.";
                return RedirectToAction(nameof(History));
            }

            var payment = await _context.Payments
                .AsNoTracking()
                .Where(p => p.PaymentReference == info.TxnRef &&
                            p.Enrollment.RegisteredByParentId == parent.Id)
                .Select(p => new { p.Id, p.EnrollmentId, p.Status })
                .SingleOrDefaultAsync();

            if (payment == null)
            {
                TempData["Error"] = "Không tìm thấy giao dịch.";
                return RedirectToAction(nameof(History));
            }

            // F5 trang kết quả sau khi đã xử lý: chỉ xem lại trạng thái.
            if (payment.Status != PaymentStatus.Pending)
            {
                return RedirectToAction("Confirmation", "Enrollment", new { id = payment.EnrollmentId });
            }

            PaymentResult result;
            try
            {
                result = await _paymentService.CompleteAsync(
                    parent.Id, payment.Id, info.IsSuccess,
                    info.TransactionNo, info.Message, info.Amount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Process VNPay return crashed.");
                TempData["Error"] = "Có lỗi hệ thống khi xử lý thanh toán. Vui lòng kiểm tra lại ở lịch sử.";
                return RedirectToAction(nameof(History));
            }

            if (!result.Success)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction("Confirmation", "Enrollment", new { id = payment.EnrollmentId });
            }

            if (result.Paid)
            {
                await TrySendPaidEmailAsync(result);
            }

            return RedirectToAction("Confirmation", "Enrollment", new { id = result.EnrollmentId });
        }

        // GET: /Payment/History
        [HttpGet]
        public async Task<IActionResult> History()
        {
            var parent = await GetCurrentParentAsync();
            if (parent == null) return Forbid();

            var items = await _context.Enrollments
                .AsNoTracking()
                .Where(e => e.RegisteredByParentId == parent.Id)
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new PaymentHistoryItemViewModel
                {
                    EnrollmentId = e.Id,
                    StudentName = e.Student.FullName,
                    ClassName = e.Class.Name,
                    Fee = e.Class.Fee,
                    Status = e.Status,
                    ReservedUntil = e.ReservedUntil,
                    CreatedAt = e.CreatedAt
                })
                .ToListAsync();

            var enrollmentIds = items.Select(i => i.EnrollmentId).ToList();

            var payments = await _context.Payments
                .AsNoTracking()
                .Where(p => enrollmentIds.Contains(p.EnrollmentId))
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            foreach (var item in items)
            {
                item.Payments = payments
                    .Where(p => p.EnrollmentId == item.EnrollmentId)
                    .Select(p => new PaymentRecordViewModel
                    {
                        Id = p.Id,
                        PaymentReference = p.PaymentReference,
                        Amount = p.Amount,
                        Status = p.Status,
                        CreatedAt = p.CreatedAt,
                        PaidAt = p.PaidAt,
                        FailureReason = p.FailureReason
                    })
                    .ToList();
            }

            return View(items);
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

        private string GetClientIp()
        {
            var ip = HttpContext.Connection.RemoteIpAddress;

            if (ip == null || IPAddress.IsLoopback(ip))
            {
                return "127.0.0.1";
            }

            if (ip.IsIPv4MappedToIPv6)
            {
                ip = ip.MapToIPv4();
            }

            return ip.ToString();
        }

        // Lỗi gửi email không làm hỏng thanh toán đã thành công.
        private async Task TrySendPaidEmailAsync(PaymentResult result)
        {
            try
            {
                var toEmail = User.FindFirstValue(ClaimTypes.Email);
                if (string.IsNullOrWhiteSpace(toEmail)) return;

                var body =
                    "<p>Thanh toán thành công. Học sinh đã chính thức được ghi danh.</p>" +
                    $"<p>Học sinh: <strong>{WebUtility.HtmlEncode(result.StudentName)}</strong><br/>" +
                    $"Lớp: <strong>{WebUtility.HtmlEncode(result.ClassName)}</strong><br/>" +
                    $"Số tiền: <strong>{result.Amount:N0} đ</strong><br/>" +
                    $"Mã giao dịch: {WebUtility.HtmlEncode(result.Reference)}</p>";

                await _emailSender.SendAsync(
                    toEmail,
                    "Xác nhận thanh toán và ghi danh - EduCenter",
                    body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send payment confirmation email.");
            }
        }
    }
}