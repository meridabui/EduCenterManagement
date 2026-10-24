
using System.ComponentModel.DataAnnotations;

namespace EduCenterManagement.ViewModels.Admin
{
    public class TimeSlotViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên khung giờ.")]
        [StringLength(50, ErrorMessage = "Tên khung giờ tối đa 50 ký tự.")]
        [Display(Name = "Tên khung giờ")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập giờ bắt đầu.")]
        [Display(Name = "Giờ bắt đầu")]
        public TimeOnly StartTime { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giờ kết thúc.")]
        [Display(Name = "Giờ kết thúc")]
        public TimeOnly EndTime { get; set; }
    }
}
