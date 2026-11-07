using EduCenterManagement.Enums;

namespace EduCenterManagement.ViewModels.Teacher
{
    public class ClassSessionItemViewModel
    {
        public int SessionId { get; set; }
        public int SessionNumber { get; set; }
        public DateOnly SessionDate { get; set; }
        public string DayOfWeekName { get; set; } = string.Empty;
        public TimeOnly StartsAt { get; set; }
        public TimeOnly EndsAt { get; set; }
        public ClassSessionStatus Status { get; set; }
        public bool IsToday { get; set; }
        public bool IsPast { get; set; }
    }

    public class ClassSessionsViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public string TeacherName { get; set; } = string.Empty;

        // Thống kê tiến độ
        public int TotalSessions { get; set; }
        public int CompletedSessions { get; set; }
        public int ScheduledSessions { get; set; }
        public int CancelledSessions { get; set; }
        public int ProgressPercentage => TotalSessions > 0 ? (int)Math.Round((double)CompletedSessions / TotalSessions * 100) : 0;

        // Buổi học hôm nay (nếu có)
        public ClassSessionItemViewModel? TodaySession { get; set; }

        // Bộ lọc hiện tại (all, today, scheduled, completed, cancelled)
        public string CurrentFilter { get; set; } = "all";

        // Danh sách các buổi học
        public List<ClassSessionItemViewModel> Sessions { get; set; } = new();
    }
}
