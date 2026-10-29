using EduCenterManagement.Enums;

namespace EduCenterManagement.ViewModels.Dashboard
{
    // Một dòng yêu cầu liên kết Parent - Student.
    public class LinkItemViewModel
    {
        public int LinkId { get; set; }

        // Parent xem: thông tin Student. Student xem: thông tin Parent.
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public ParentStudentLinkStatus Status { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? RespondedAt { get; set; }
    }

    public class NotificationItemViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class ParentDashboardViewModel
    {
        public string FullName { get; set; } = string.Empty;
        public string LinkingCode { get; set; } = string.Empty;

        public List<LinkItemViewModel> PendingRequests { get; set; } = new();
        public List<LinkItemViewModel> LinkedStudents { get; set; } = new();
        public List<NotificationItemViewModel> Notifications { get; set; } = new();
    }

    public class StudentDashboardViewModel
    {
        public string FullName { get; set; } = string.Empty;

        public List<LinkItemViewModel> Links { get; set; } = new();
        public List<NotificationItemViewModel> Notifications { get; set; } = new();
    }
}
