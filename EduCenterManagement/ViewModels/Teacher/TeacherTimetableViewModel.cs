using EduCenterManagement.Enums;

namespace EduCenterManagement.ViewModels.Teacher
{
    public class TeacherTimetableViewModel
    {
        public string TeacherName { get; set; } = string.Empty;
        public DateOnly WeekStartDate { get; set; }
        public DateOnly WeekEndDate { get; set; }
        public int CurrentWeekOffset { get; set; }

        // Danh sách 7 ngày trong tuần
        public List<DateOnly> DaysOfWeek { get; set; } = new();

        // Danh sách các Ca học (Slot 1, Slot 2...)
        public List<TeacherTimeSlotHeaderViewModel> TimeSlots { get; set; } = new();

        // Danh sách các buổi dạy trong tuần
        public List<TeacherTimetableSessionViewModel> Sessions { get; set; } = new();
    }

    public class TeacherTimeSlotHeaderViewModel
    {
        public int SlotId { get; set; }
        public string Name { get; set; } = string.Empty;
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
    }

    public class TeacherTimetableSessionViewModel
    {
        public int? SessionId { get; set; }
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public DateOnly SessionDate { get; set; }
        public TimeOnly StartsAt { get; set; }
        public TimeOnly EndsAt { get; set; }
        public int MatchedTimeSlotId { get; set; }
        public int CurrentStudents { get; set; }
        public int MaxStudents { get; set; }
        public ClassSessionStatus? SessionStatus { get; set; }
        public bool IsActualSession { get; set; }
    }
}
