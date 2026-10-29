using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduCenterManagement.ViewModels.Teacher.Quiz
{
    public class CloneQuizViewModel
    {
        public int SourceQuizId { get; set; }
        public string SourceQuizTitle { get; set; } = string.Empty;
        public string SourceTeacherName { get; set; } = string.Empty;
        public string SourceSubjectName { get; set; } = string.Empty;
        public int QuestionCount { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn lớp học mục tiêu.")]
        [Display(Name = "Lớp học mục tiêu")]
        public int TargetClassId { get; set; }

        [Display(Name = "Buổi học (Tùy chọn)")]
        public int? TargetSessionId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề cho bài Quiz mới.")]
        [MaxLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự.")]
        [Display(Name = "Tiêu đề bài Quiz mới")]
        public string NewTitle { get; set; } = string.Empty;

        [Range(1, 1440)]
        [Display(Name = "Thời lượng (phút)")]
        public int DurationMinutes { get; set; } = 15;

        [Display(Name = "Thời gian mở")]
        public DateTime AvailableFrom { get; set; } = DateTime.Now;

        [Display(Name = "Thời gian đóng")]
        public DateTime AvailableUntil { get; set; } = DateTime.Now.AddDays(7);

        public List<SelectListItem> TargetClasses { get; set; } = new();
        public List<SelectListItem> TargetSessions { get; set; } = new();
    }
}
