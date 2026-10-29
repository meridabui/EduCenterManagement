using System.ComponentModel.DataAnnotations;
using EduCenterManagement.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduCenterManagement.ViewModels.Teacher.Quiz
{
    public class CreateQuizViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn lớp học.")]
        [Display(Name = "Lớp học")]
        public int ClassId { get; set; }

        [Display(Name = "Buổi học (Tùy chọn)")]
        public int? SessionId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề bài Quiz.")]
        [MaxLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự.")]
        [Display(Name = "Tiêu đề bài Quiz")]
        public string Title { get; set; } = string.Empty;

        [Range(1, 1440, ErrorMessage = "Thời lượng làm bài phải từ 1 đến 1440 phút.")]
        [Display(Name = "Thời lượng (phút)")]
        public int DurationMinutes { get; set; } = 15;

        [Required(ErrorMessage = "Vui lòng chọn thời gian bắt đầu mở bài thi.")]
        [Display(Name = "Thời gian mở")]
        public DateTime AvailableFrom { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Vui lòng chọn thời gian kết thúc bài thi.")]
        [Display(Name = "Thời gian đóng")]
        public DateTime AvailableUntil { get; set; } = DateTime.Now.AddDays(7);

        [Range(1, 100, ErrorMessage = "Số lần làm bài tối đa phải từ 1 đến 100.")]
        [Display(Name = "Số lần làm tối đa")]
        public int MaxAttempts { get; set; } = 1;

        [Display(Name = "Trạng thái")]
        public QuizStatus Status { get; set; } = QuizStatus.Draft;

        // Dữ liệu hỗ trợ dropdown
        public List<SelectListItem> Classes { get; set; } = new();
        public List<SelectListItem> Sessions { get; set; } = new();
    }
}
