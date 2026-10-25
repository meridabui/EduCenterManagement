
using System.ComponentModel.DataAnnotations;

namespace EduCenterManagement.ViewModels.Admin
{
    public class AssignTeacherViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn lớp học.")]
        [Range(1, int.MaxValue, ErrorMessage = "Lớp học không hợp lệ.")]
        [Display(Name = "Lớp học")]
        public int ClassId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn giáo viên.")]
        [Range(1, int.MaxValue, ErrorMessage = "Giáo viên không hợp lệ.")]
        [Display(Name = "Giáo viên")]
        public int TeacherId { get; set; }
    }
}
