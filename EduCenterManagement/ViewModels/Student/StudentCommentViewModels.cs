namespace EduCenterManagement.ViewModels.Student
{
    public class StudentCommentListViewModel
    {
        public int? SelectedClassId { get; set; }

        public List<StudentCommentClassOption> Classes { get; set; } = new();

        public List<StudentCommentItemViewModel> Comments { get; set; } = new();
    }

    public class StudentCommentClassOption
    {
        public int ClassId { get; set; }

        public string ClassName { get; set; } = string.Empty;

        public int CommentCount { get; set; }
    }

    public class StudentCommentItemViewModel
    {
        public int Id { get; set; }

        public string Content { get; set; } = string.Empty;

        public string TeacherName { get; set; } = string.Empty;

        public int ClassId { get; set; }

        public string ClassName { get; set; } = string.Empty;

        public string? SubjectName { get; set; }

        public int? SessionNumber { get; set; }

        public DateOnly? SessionDate { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public bool IsEdited => UpdatedAt > CreatedAt.AddMinutes(1);
    }
}
