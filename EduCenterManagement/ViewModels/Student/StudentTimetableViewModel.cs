using EduCenterManagement.Enums;

namespace EduCenterManagement.ViewModels.Student
{
    public class WeeklyTimetableViewModel
    {
        public DateOnly WeekStartDate { get; set; }
        public DateOnly WeekEndDate { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public int UnreadNotificationCount { get; set; }

        // Danh sách 7 ngày trong tuần
        public List<DateOnly> DaysOfWeek { get; set; } = new();

        // Danh sách các Ca học (Slot 1, Slot 2...)
        public List<TimeSlotHeaderViewModel> TimeSlots { get; set; } = new();

        // Danh sách các buổi học được gán vào thời khóa biểu
        public List<TimetableSessionItemViewModel> Sessions { get; set; } = new();
    }

    public class TimeSlotHeaderViewModel
    {
        public int SlotId { get; set; }
        public string Name { get; set; } = string.Empty;
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
    }

    public class TimetableSessionItemViewModel
    {
        public int SessionId { get; set; }
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public DateOnly SessionDate { get; set; }
        public TimeOnly StartsAt { get; set; }
        public TimeOnly EndsAt { get; set; }
        public int SessionNumber { get; set; }
        public ClassSessionStatus Status { get; set; }

        // Khung giờ tương ứng
        public int? MatchedTimeSlotId { get; set; }

        // Trạng thái điểm danh của học sinh
        public AttendanceStatus? AttendanceStatus { get; set; }
        public bool IsAttendanceMarked => AttendanceStatus.HasValue;

        // Trạng thái buổi học: Đã diễn ra / Chưa diễn ra
        public bool HasTakenPlace { get; set; }

        // Slide & Quiz
        public int SlideCount { get; set; }
        public int? FirstSlideId { get; set; }
        public int QuizCount { get; set; }
        public int? FirstQuizId { get; set; }
        public bool IsQuizTaken { get; set; }
    }

    public class ClassDetailViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string SubjectDescription { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public decimal Fee { get; set; }
        public ClassStatus Status { get; set; }

        public List<SessionMaterialViewModel> Sessions { get; set; } = new();
    }

    public class SessionMaterialViewModel
    {
        public int SessionId { get; set; }
        public int SessionNumber { get; set; }
        public DateOnly SessionDate { get; set; }
        public TimeOnly StartsAt { get; set; }
        public TimeOnly EndsAt { get; set; }
        public ClassSessionStatus Status { get; set; }
        public List<SlideItemViewModel> Slides { get; set; } = new();
    }

    public class SlideItemViewModel
    {
        public int SlideId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? OriginalFileName { get; set; }
        public long? FileSize { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}
