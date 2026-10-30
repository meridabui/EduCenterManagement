using EduCenterManagement.Enums;

namespace EduCenterManagement.ViewModels.Teacher.Attendance
{
    public class StudentAttendanceItemViewModel
    {
        public int StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public DateOnly? DateOfBirth { get; set; }

        // Giá trị mặc định là Absent (Vắng mặt)
        public AttendanceStatus Status { get; set; } = AttendanceStatus.Absent;

        public string? Note { get; set; }
        public bool HasExistingRecord { get; set; }
    }

    public class AttendanceSheetViewModel
    {
        public int SessionId { get; set; }
        public int SessionNumber { get; set; }
        public DateOnly SessionDate { get; set; }
        public string DayOfWeekName { get; set; } = string.Empty;
        public TimeOnly StartsAt { get; set; }
        public TimeOnly EndsAt { get; set; }
        public ClassSessionStatus SessionStatus { get; set; }

        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;

        public bool MarkSessionAsCompleted { get; set; } = false;

        public List<StudentAttendanceItemViewModel> Students { get; set; } = new();

        public int TotalStudents => Students.Count;
        public int PresentCount => Students.Count(s => s.Status == AttendanceStatus.Present);
        public int AbsentCount => Students.Count(s => s.Status == AttendanceStatus.Absent);
    }
}

