
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
        [BindNever]
        public List<TimeSlotClassUsageViewModel> UsedByClasses { get; set; } = new();

        public bool IsTimeLocked => UsedByClasses.Count > 0;
    }

    public class TimeSlotClassUsageViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
