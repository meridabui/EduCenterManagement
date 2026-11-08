using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Services
{
    public class PaymentResult
    {
        public bool Success { get; private set; }
        public string? Error { get; private set; }
        public int PaymentId { get; private set; }
        public int EnrollmentId { get; private set; }
        public bool Paid { get; private set; }
        public decimal Amount { get; private set; }
        public string Reference { get; private set; } = string.Empty;
        public string StudentName { get; private set; } = string.Empty;
        public string ClassName { get; private set; } = string.Empty;

        public static PaymentResult Fail(string error)
        {
            return new PaymentResult { Success = false, Error = error };
        }

        public static PaymentResult Ok(
            int paymentId, int enrollmentId, bool paid = false,
            decimal amount = 0, string reference = "",
            string studentName = "", string className = "")
        {
            return new PaymentResult
            {
                Success = true,
                PaymentId = paymentId,
                EnrollmentId = enrollmentId,
                Paid = paid,
                Amount = amount,
                Reference = reference,
                StudentName = studentName,
                ClassName = className
            };
        }
    }

    public interface IPaymentService
    {
        // Tạo (hoặc dùng lại) Payment Pending cho Enrollment đang giữ chỗ.
        Task<PaymentResult> StartCheckoutAsync(int parentId, int enrollmentId);

        // Xử lý kết quả từ cổng: Paid -> xác nhận Enrollment; Failed/quá hạn -> hủy và nhả chỗ.
        Task<PaymentResult> CompleteAsync(
    int parentId, int paymentId, bool succeeded,
    string? gatewayTransactionId = null,
    string? gatewayMessage = null,
    decimal? paidAmount = null);
    }

    public class PaymentService : IPaymentService
    {
        public const string GatewayName = "MockPay";

        private readonly ApplicationDbContext _context;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(ApplicationDbContext context, ILogger<PaymentService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<PaymentResult> StartCheckoutAsync(int parentId, int enrollmentId)
        {
            var now = DateTime.UtcNow;

            var enrollment = await _context.Enrollments
                .Include(e => e.Class)
                .SingleOrDefaultAsync(e =>
                    e.Id == enrollmentId && e.RegisteredByParentId == parentId);

            if (enrollment == null)
            {
                return PaymentResult.Fail("Không tìm thấy đăng ký.");
            }

            if (enrollment.Status != EnrollmentStatus.PendingPayment ||
                enrollment.ReservedUntil == null ||
                enrollment.ReservedUntil <= now)
            {
                return PaymentResult.Fail(
                    "Đăng ký đã hết thời gian giữ chỗ hoặc không còn chờ thanh toán.");
            }

            if (enrollment.Class.Fee <= 0)
            {
                return PaymentResult.Fail("Học phí của lớp không hợp lệ.");
            }

            // Bấm Thanh toán nhiều lần chỉ dùng chung một Payment Pending.
            var existing = await _context.Payments.FirstOrDefaultAsync(p =>
                p.EnrollmentId == enrollmentId && p.Status == PaymentStatus.Pending);

            if (existing != null)
            {
                return PaymentResult.Ok(existing.Id, enrollmentId);
            }

            var payment = new Payment
            {
                EnrollmentId = enrollmentId,
                Amount = enrollment.Class.Fee,
                Currency = "VND",
                Status = PaymentStatus.Pending,
                Gateway = GatewayName,
                PaymentReference = $"EDU{enrollmentId}-{Guid.NewGuid():N}".Substring(0, 24),
                ExpiresAt = enrollment.ReservedUntil,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Payments.Add(payment);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Create payment failed (enrollment {EnrollmentId})", enrollmentId);
                return PaymentResult.Fail("Không thể tạo giao dịch. Vui lòng thử lại.");
            }

            return PaymentResult.Ok(payment.Id, enrollmentId);
        }

        public async Task<PaymentResult> CompleteAsync(int parentId, int paymentId, bool succeeded, string? gatewayTransactionId = null, string? gatewayMessage = null,decimal? paidAmount = null)
        {
            var now = DateTime.UtcNow;
            DateTime? nowValue = now;

            await using var transaction = await _context.Database.BeginTransactionAsync();

            // Khóa dòng Payment để hai request cùng lúc không xử lý một giao dịch hai lần.
            var payment = await _context.Payments
                .FromSqlInterpolated(
                    $"SELECT * FROM payments WITH (UPDLOCK, ROWLOCK) WHERE Id = {paymentId}")
                .SingleOrDefaultAsync();

            if (payment == null)
            {
                return PaymentResult.Fail("Không tìm thấy giao dịch.");
            }

            var info = await _context.Enrollments
                .AsNoTracking()
                .Where(e => e.Id == payment.EnrollmentId && e.RegisteredByParentId == parentId)
                .Select(e => new
                {
                    e.Id,
                    StudentName = e.Student.FullName,
                    ClassName = e.Class.Name,
                    ParentUserId = e.RegisteredByParent.UserId
                })
                .SingleOrDefaultAsync();

            // Giao dịch của người khác cũng trả cùng thông báo này.
            if (info == null)
            {
                return PaymentResult.Fail("Không tìm thấy giao dịch.");
            }

            if (payment.Status != PaymentStatus.Pending)
            {
                return PaymentResult.Fail("Giao dịch này đã được xử lý trước đó.");
            }

            var paid = false;
            string? failureReason = null;

            // Cổng thật: số tiền cổng đã thu phải khớp học phí.
            if (succeeded && paidAmount.HasValue && paidAmount.Value != payment.Amount)
            {
                succeeded = false;
                failureReason = "Số tiền thanh toán không khớp với học phí.";
            }

            if (succeeded)
            {
                // Chỉ xác nhận khi Enrollment vẫn Pending và còn hạn giữ chỗ.
                // Nếu worker vừa hủy thì 0 dòng bị ảnh hưởng.
                var confirmed = await _context.Enrollments
                    .Where(e =>
                        e.Id == payment.EnrollmentId &&
                        e.Status == EnrollmentStatus.PendingPayment &&
                        e.ReservedUntil > now)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(e => e.Status, EnrollmentStatus.Confirm)
                        .SetProperty(e => e.ConfirmedAt, nowValue)
                        .SetProperty(e => e.UpdatedAt, now));

                paid = confirmed > 0;

                if (!paid)
                {
                    failureReason = "Hết thời gian giữ chỗ trước khi thanh toán hoàn tất.";
                }
            }
            else
            {
                failureReason ??= gatewayMessage ?? "Thanh toán không thành công.";
            }

            if (!paid)
            {
                // Hủy Enrollment: chỗ được nhả vì đếm chỗ chỉ tính Confirm và Pending còn hạn.
                await _context.Enrollments
                    .Where(e =>
                        e.Id == payment.EnrollmentId &&
                        e.Status == EnrollmentStatus.PendingPayment)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(e => e.Status, EnrollmentStatus.PaymentFailed)
                        .SetProperty(e => e.CancelledAt, nowValue)
                        .SetProperty(e => e.UpdatedAt, now));
            }

            payment.Status = paid ? PaymentStatus.Paid : PaymentStatus.Failed;
            payment.UpdatedAt = now;

            if (paid)
            {
                payment.PaidAt = now;
                payment.GatewayTransactionId = gatewayTransactionId ?? $"MOCK-{Guid.NewGuid():N}";
            }
            else
            {
                payment.FailureReason = failureReason;
            }

            _context.Notifications.Add(new Notification
            {
                UserId = info.ParentUserId,
                Type = paid ? "PaymentPaid" : "PaymentFailed",
                Title = paid ? "Thanh toán thành công" : "Thanh toán thất bại",
                Message = paid
                    ? $"Học sinh {info.StudentName} đã được ghi danh vào lớp {info.ClassName}."
                    : $"Đăng ký lớp {info.ClassName} cho {info.StudentName} đã bị hủy: {failureReason}",
                ReferenceType = "Enrollment",
                ReferenceId = info.Id,
                CreatedAt = now
            });

            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Complete payment failed (payment {PaymentId})", paymentId);
                return PaymentResult.Fail("Không thể ghi nhận kết quả thanh toán. Vui lòng thử lại.");
            }

            return PaymentResult.Ok(
                payment.Id, payment.EnrollmentId, paid,
                payment.Amount, payment.PaymentReference ?? string.Empty,
                info.StudentName, info.ClassName);
        }
    }
}