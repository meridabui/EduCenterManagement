using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EduCenterManagement.ViewModels.Teacher.Slide
{
    public class UploadSlideInputModel
    {
        [Required]
        public int SessionId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề tài liệu / slide.")]
        [StringLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự.")]
        [Display(Name = "Tiêu đề tài liệu")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn tệp tin cần tải lên.")]
        [Display(Name = "Tệp tài liệu (PDF, PPTX, PPT, DOCX, ZIP, ...)")]
        public IFormFile? File { get; set; }
    }
}

