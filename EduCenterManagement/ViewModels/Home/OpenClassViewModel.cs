
namespace EduCenterManagement.ViewModels.Home
{
    public class OpenClassViewModel
    {
        public int Id { get; set; }

        public string ClassCode { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;
        public string? TeacherName { get; set; }

        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }

        public int MaxStudents { get; set; }

        public List<OpenClassScheduleViewModel> Schedules { get; set; } = new();
    }

    public class OpenClassScheduleViewModel
    {
        public int DayOfWeek { get; set; }
        public string TimeSlotName { get; set; } = string.Empty;
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
    }
}
