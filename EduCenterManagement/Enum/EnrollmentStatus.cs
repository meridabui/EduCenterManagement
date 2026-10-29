namespace EduCenterManagement.Enums
{
    // Giữ nguyên giá trị số cũ (lưu tinyint) nên không cần migration cho việc đổi tên.
    public enum EnrollmentStatus : byte
    { 
       PendingPayment = 1,
       Confirm = 2,
       Expried = 3,
       PaymentFailed = 4,
       Cancelled = 5,
    }
}
