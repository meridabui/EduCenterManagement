using EduCenterManagement.Enums;

namespace EduCenterManagement.ViewModels.Payments
{
    public class PaymentGatewayViewModel
    {
        public int PaymentId { get; set; }
        public string PaymentReference { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "VND";
        public string StudentName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;

        // Kind = Utc để JavaScript đếm ngược chính xác.
        public DateTime ExpiresAt { get; set; }
    }

    public class PaymentRecordViewModel
    {
        public int Id { get; set; }
        public string? PaymentReference { get; set; }
        public decimal Amount { get; set; }
        public PaymentStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? FailureReason { get; set; }
    }

    public class PaymentHistoryItemViewModel
    {
        public int EnrollmentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public decimal Fee { get; set; }
        public EnrollmentStatus Status { get; set; }
        public DateTime? ReservedUntil { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<PaymentRecordViewModel> Payments { get; set; } = new();
    }
}