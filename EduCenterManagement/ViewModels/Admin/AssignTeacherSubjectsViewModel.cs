using System.ComponentModel.DataAnnotations;

namespace EduCenterManagement.ViewModels.Admin
{
    public class AssignTeacherSubjectsViewModel
    {
        [Range(1, int.MaxValue)]
        public int TeacherId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ít nhất một môn học.")]
        [MinLength(1, ErrorMessage = "Vui lòng chọn ít nhất một môn học.")]
        [Display(Name = "Môn được phân công")]
        public List<int> SubjectIds { get; set; } = new();
    }
}
