
using System.ComponentModel.DataAnnotations;
using EduCenterManagement.Enums;

namespace EduCenterManagement.ViewModels.Admin
{
    public class SubjectViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên môn học.")]
        [Display(Name = "Tên môn học")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Display(Name = "Trạng thái")]
        public SubjectStatus Status { get; set; } = SubjectStatus.Active;
    }
}
