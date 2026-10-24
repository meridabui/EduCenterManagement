
using System.ComponentModel.DataAnnotations;

namespace EduCenterManagement.ViewModels.Admin
{
    public class ClassScheduleViewModel : IValidatableObject
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn lớp học.")]
        [Range(1, int.MaxValue, ErrorMessage = "Lớp học không hợp lệ.")]
        [Display(Name = "Lớp học")]
        public int ClassId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn khung giờ.")]
        [Range(1, int.MaxValue, ErrorMessage = "Khung giờ không hợp lệ.")]
        [Display(Name = "Khung giờ")]
        public int TimeSlotId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn thứ trong tuần.")]
        [Range(1, 7, ErrorMessage = "Thứ phải từ 1 đến 7.")]
        [Display(Name = "Thứ trong tuần")]
        public int DayOfWeek { get; set; }

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (DayOfWeek < 1 || DayOfWeek > 7)
            {
                yield return new ValidationResult(
                    "Thứ phải từ 1 đến 7.",
                    new[] { nameof(DayOfWeek) });
            }
        }
    }
}
