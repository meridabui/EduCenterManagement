namespace EduCenterManagement.Enums
{
    public enum EnrollmentStatus : byte
    {
        PendingPayment = 1,
        Confirmed = 2,
        Expired = 3,
        PaymentFailed = 4,
        Cancelled = 5
    }
}