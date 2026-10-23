
namespace EduCenterManagement.Enums
{
    public enum EmailOutboxStatus : byte
    {
        Pending = 1,
        Processing = 2,
        Sent = 3,
        Failed = 4,
        Cancelled = 5
    }
}
