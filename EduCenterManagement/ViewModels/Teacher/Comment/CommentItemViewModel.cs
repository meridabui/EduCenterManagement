namespace EduCenterManagement.ViewModels.Teacher.Comment
{
    public class CommentItemViewModel
    {
        public int Id { get; set; }
        public int TeacherId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Visibility { get; set; } = "parent_student";
        public int? SessionId { get; set; }
        public string? SessionLabel { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public string VisibilityBadgeClass => Visibility == "teacher_only" 
            ? "badge bg-warning text-dark" 
            : "badge bg-success";

        public string VisibilityText => Visibility == "teacher_only" 
            ? "Chỉ nội bộ Giáo viên" 
            : "Phụ huynh & Học sinh cùng xem";
    }
}

