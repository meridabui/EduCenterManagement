
using System.ComponentModel.DataAnnotations;

namespace EduCenterManagement.ViewModels.Admin
{
    public class ClassScheduleInputViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn thứ học.")]
        [Range(2, 8, ErrorMessage = "Thứ học không hợp lệ.")]
        [Display(Name = "Thứ trong tuần")]
        public int DayOfWeek { get; set; } = 2;

        [Required(ErrorMessage = "Vui lòng chọn khung giờ.")]
        [Range(1, int.MaxValue, ErrorMessage = "Khung giờ không hợp lệ.")]
        [Display(Name = "Khung giờ")]
        public int TimeSlotId { get; set; }
    }
}
