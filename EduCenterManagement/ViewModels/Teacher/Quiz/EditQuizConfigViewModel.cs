using System.ComponentModel.DataAnnotations;

namespace EduCenterManagement.ViewModels.Teacher.Quiz
{
    public class EditQuizConfigViewModel
    {
        public int QuizId { get; set; }

        [Required(ErrorMessage = "Tiêu đề bài Quiz không được để trống.")]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Range(1, 300, ErrorMessage = "Thời lượng làm bài từ 1 đến 300 phút.")]
        public int DurationMinutes { get; set; } = 15;

        [Required(ErrorMessage = "Vui lòng chọn thời gian bắt đầu.")]
        public DateTime AvailableFrom { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn thời gian kết thúc.")]
        public DateTime AvailableUntil { get; set; }

        [Range(1, 100, ErrorMessage = "Số lần làm bài tối đa từ 1 đến 100.")]
        public int MaxAttempts { get; set; } = 1;
    }
}

