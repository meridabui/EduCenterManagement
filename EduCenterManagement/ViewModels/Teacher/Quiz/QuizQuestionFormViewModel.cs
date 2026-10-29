using System.ComponentModel.DataAnnotations;

namespace EduCenterManagement.ViewModels.Teacher.Quiz
{
    public class QuizQuestionFormViewModel
    {
        public int QuizId { get; set; }
        public int QuestionId { get; set; } // 0 nếu là tạo mới

        [Required(ErrorMessage = "Vui lòng nhập nội dung câu hỏi.")]
        [Display(Name = "Nội dung câu hỏi")]
        public string QuestionText { get; set; } = string.Empty;

        [Range(typeof(decimal), "0.01", "100.00", ErrorMessage = "Điểm câu hỏi phải từ 0.01 đến 100 điểm.")]
        [Display(Name = "Điểm câu hỏi")]
        public decimal Points { get; set; } = 1.00m;

        [Range(1, int.MaxValue)]
        public int SortOrder { get; set; } = 1;

        // 4 phương án trắc nghiệm chuẩn A, B, C, D
        public List<QuizOptionInputModel> Options { get; set; } = new()
        {
            new QuizOptionInputModel { Label = "A", SortOrder = 1 },
            new QuizOptionInputModel { Label = "B", SortOrder = 2 },
            new QuizOptionInputModel { Label = "C", SortOrder = 3 },
            new QuizOptionInputModel { Label = "D", SortOrder = 4 }
        };

        [Required(ErrorMessage = "Vui lòng chọn đáp án đúng.")]
        public int CorrectOptionIndex { get; set; } = 0; // 0: A, 1: B, 2: C, 3: D
    }

    public class QuizOptionInputModel
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty; // A, B, C, D

        [Required(ErrorMessage = "Nội dung đáp án không được để trống.")]
        public string OptionText { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsCorrect { get; set; }
    }
}

