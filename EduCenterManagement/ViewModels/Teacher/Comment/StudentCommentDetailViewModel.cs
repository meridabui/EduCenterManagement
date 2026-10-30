using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduCenterManagement.ViewModels.Teacher.Comment
{
    public class StudentCommentDetailViewModel
    {
        public int StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public DateOnly? DateOfBirth { get; set; }

        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;

        public List<CommentItemViewModel> Comments { get; set; } = new();
        public List<SelectListItem> Sessions { get; set; } = new();

        public CreateCommentInputModel NewComment { get; set; } = new();
    }
}

