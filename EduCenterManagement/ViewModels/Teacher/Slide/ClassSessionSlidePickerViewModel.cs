using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduCenterManagement.ViewModels.Teacher.Slide
{
    public class ClassSessionSlidePickerViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn lớp học.")]
        [Display(Name = "Lớp học")]
        public int? SelectedClassId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn buổi học.")]
        [Display(Name = "Buổi học")]
        public int? SelectedSessionId { get; set; }

        public string TeacherName { get; set; } = string.Empty;

        public List<SelectListItem> Classes { get; set; } = new();
        public List<SelectListItem> Sessions { get; set; } = new();
    }
}

