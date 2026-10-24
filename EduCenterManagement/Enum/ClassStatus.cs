
namespace EduCenterManagement.Enums
{
    public enum ClassStatus : byte
    {
        Draft = 1, //Admin đã tạo lớp nhưng lớp chưa sẵn sàng mở đăng ký
        Open = 2, //Lớp có lịch học hợp lệ, đã phân công Teacher và còn chỗ
        InProgress = 3,
        Completed = 4,
        Cancelled = 5
    }
}
