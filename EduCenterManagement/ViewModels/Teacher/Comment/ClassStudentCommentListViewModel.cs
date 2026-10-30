using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduCenterManagement.ViewModels.Teacher.Comment
{
    public class StudentCommentSummaryItem
    {
        public int StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public DateOnly? DateOfBirth { get; set; }
        public int CommentCount { get; set; }
        public string? LatestCommentContent { get; set; }
        public DateTime? LatestCommentAt { get; set; }
    }

    public class ClassStudentCommentListViewModel
    {
        public int? SelectedClassId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public List<SelectListItem> Classes { get; set; } = new();

        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public List<StudentCommentSummaryItem> Students { get; set; } = new();
    }
}

