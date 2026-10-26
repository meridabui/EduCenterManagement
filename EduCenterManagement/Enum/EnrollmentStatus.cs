namespace EduCenterManagement.Enums
{
    // Giữ nguyên giá trị số cũ (lưu tinyint) nên không cần migration cho việc đổi tên.
    public enum EnrollmentStatus : byte
    {
        Pending = 1,   // Đã tạo Enrollment, giữ chỗ tạm thời chờ thanh toán (BR15)
        Paid = 2,      // Thanh toán thành công, đăng ký được xác nhận (BR18)
        Failed = 4,    // Thanh toán thất bại, nhả chỗ ngay (BR19)
        Cancelled = 5  // Quá thời gian giữ chỗ mà chưa thanh toán (BR16)
    }
}
