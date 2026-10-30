using System.ComponentModel.DataAnnotations;

namespace EduCenterManagement.ViewModels.Teacher.Comment
{
    public class CreateCommentInputModel
    {
        [Required]
        public int StudentId { get; set; }

        [Required]
        public int ClassId { get; set; }

        [Display(Name = "Buổi học liên quan (tùy chọn)")]
        public int? SessionId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung nhận xét.")]
        [StringLength(2000, ErrorMessage = "Nội dung nhận xét không được vượt quá 2000 ký tự.")]
        [Display(Name = "Nội dung nhận xét")]
        public string Content { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Phạm vi hiển thị")]
        public string Visibility { get; set; } = "parent_student";
    }

    public class EditCommentInputModel
    {
        [Required]
        public int CommentId { get; set; }

        [Required]
        public int ClassId { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Display(Name = "Buổi học liên quan (tùy chọn)")]
        public int? SessionId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung nhận xét.")]
        [StringLength(2000, ErrorMessage = "Nội dung nhận xét không được vượt quá 2000 ký tự.")]
        [Display(Name = "Nội dung nhận xét")]
        public string Content { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Phạm vi hiển thị")]
        public string Visibility { get; set; } = "parent_student";
    }
}

