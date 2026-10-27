using EduCenterManagement.Enums;

namespace EduCenterManagement.ViewModels.Registration
{
    public class StudentOptionViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
    }

    public class ClassScheduleItemViewModel
    {
        public int DayOfWeek { get; set; }
        public string TimeSlotName { get; set; } = string.Empty;
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
    }

    public class ClassOptionViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string? TeacherName { get; set; }
        public string? PrerequisiteName { get; set; }

        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public decimal Fee { get; set; }

        public int MaxStudents { get; set; }
        public int SeatsLeft { get; set; }

        // Học sinh đang chọn đã có đăng ký còn hiệu lực với lớp này.
        public bool AlreadyRegistered { get; set; }

        public List<ClassScheduleItemViewModel> Schedules { get; set; } = new();
    }

    public class EnrollmentIndexViewModel
    {
        public List<StudentOptionViewModel> Students { get; set; } = new();
        public int? SelectedStudentId { get; set; }
        public List<ClassOptionViewModel> Classes { get; set; } = new();
    }

    public class EnrollmentConfirmationViewModel
    {
        public int EnrollmentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public decimal Fee { get; set; }

        public EnrollmentStatus Status { get; set; }

        // Giờ UTC (Kind = Utc) để JavaScript đếm ngược chính xác.
        public DateTime? ReservedUntil { get; set; }

        // Pending và còn trong thời gian giữ chỗ.
        public bool IsHoldActive { get; set; }
    }
}
