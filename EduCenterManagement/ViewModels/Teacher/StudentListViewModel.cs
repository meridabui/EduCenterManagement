namespace EduCenterManagement.ViewModels.Teacher
{
    public class StudentItemViewModel
    {
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public DateOnly? DateOfBirth { get; set; }
    }

    public class StudentListViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public List<StudentItemViewModel> Students { get; set; } = new();
    }
}
