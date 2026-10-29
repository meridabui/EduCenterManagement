using EduCenterManagement.Enums;

namespace EduCenterManagement.ViewModels.Teacher
{
    public class AssignedClassItemViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public int CurrentStudents { get; set; }
        public int MaxStudents { get; set; }
        public decimal Fee { get; set; }
        public ClassStatus Status { get; set; }
        public List<string> Schedules { get; set; } = new();
    }

    public class AssignedClassesViewModel
    {
        public string TeacherName { get; set; } = string.Empty;
        public List<AssignedClassItemViewModel> Classes { get; set; } = new();
    }
}
