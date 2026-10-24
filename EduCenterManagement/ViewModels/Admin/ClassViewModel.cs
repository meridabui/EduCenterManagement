
using System.ComponentModel.DataAnnotations;
using EduCenterManagement.Enums;

namespace EduCenterManagement.ViewModels.Admin
{
    public class ClassViewModel : IValidatableObject
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn môn học.")]
        [Display(Name = "Môn học")]
        [Range(1, int.MaxValue, ErrorMessage = "Môn học không hợp lệ.")]
        public int SubjectId { get; set; }

        [Display(Name = "Giáo viên phụ trách")]
        public int? TeacherId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên lớp.")]
        [StringLength(100, ErrorMessage = "Tên lớp tối đa 100 ký tự.")]
        [Display(Name = "Tên lớp")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu.")]
        [Display(Name = "Ngày bắt đầu")]
        public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc.")]
        [Display(Name = "Ngày kết thúc")]
        public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Range(1, 1000, ErrorMessage = "Sĩ số tối đa phải từ 1 đến 1000.")]
        [Display(Name = "Sĩ số tối đa")]
        public int MaxStudents { get; set; } = 20;

        [Range(typeof(decimal), "0", "9999999999999999",
            ErrorMessage = "Học phí phải lớn hơn hoặc bằng 0.")]
        [Display(Name = "Học phí")]
        public decimal Fee { get; set; }

        [Display(Name = "Trạng thái")]
        public ClassStatus Status { get; set; } = ClassStatus.Draft;

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (EndDate < StartDate)
            {
                yield return new ValidationResult(
                    "Ngày kết thúc phải bằng hoặc sau ngày bắt đầu.",
                    new[] { nameof(EndDate) });
            }
        }
    }
}
