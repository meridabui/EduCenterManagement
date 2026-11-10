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
        public int SubjectId { get; set; }
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
        public int? ActiveEnrollmentId { get; set; }
        public EnrollmentStatus? ActiveEnrollmentStatus { get; set; }
        public bool HasPendingEnrollment =>
            ActiveEnrollmentStatus == EnrollmentStatus.PendingPayment;
        public bool AlreadyConfirmed =>
            ActiveEnrollmentStatus == EnrollmentStatus.Confirm;

        public List<ClassScheduleItemViewModel> Schedules { get; set; } = new();
    }

    public class SubjectOptionViewModel
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int OpenClassCount { get; set; }
        public int AvailableClassCount { get; set; }
        public int TotalSeatsLeft { get; set; }
        public DateOnly EarliestStartDate { get; set; }
        public decimal MinimumFee { get; set; }
    }

    public class EnrollmentIndexViewModel
    {
        public List<StudentOptionViewModel> Students { get; set; } = new();
        public int? SelectedStudentId { get; set; }
        public int? SelectedSubjectId { get; set; }
        public List<SubjectOptionViewModel> Subjects { get; set; } = new();
        public List<ClassOptionViewModel> Classes { get; set; } = new();
    }

    public class EnrollmentClassDetailsViewModel
    {
        public List<StudentOptionViewModel> Students { get; set; } = new();
        public int? SelectedStudentId { get; set; }
        public ClassOptionViewModel Class { get; set; } = new();
    }

    public class EnrollmentConfirmationViewModel
    {
        public int EnrollmentId { get; set; }
        public int StudentId { get; set; }
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

    public class EnrollmentHistoryItemViewModel
    {
        public int EnrollmentId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public decimal Fee { get; set; }
        public EnrollmentStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReservedUntil { get; set; }
        public DateTime? ConfirmedAt { get; set; }
    }

    public class EnrollmentHistoryViewModel
    {
        public List<StudentOptionViewModel> Students { get; set; } = new();
        public int? SelectedStudentId { get; set; }
        public EnrollmentStatus? SelectedStatus { get; set; }
        public List<EnrollmentHistoryItemViewModel> Items { get; set; } = new();
    }

}
