
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using EduCenterManagement.Enums;

namespace EduCenterManagement.ViewModels.Admin
{
    public class ClassViewModel : IValidatableObject
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn môn học.")]
        [Range(1, int.MaxValue, ErrorMessage = "Môn học không hợp lệ.")]
        [Display(Name = "Môn học")]
        public int SubjectId { get; set; }

        [Display(Name = "Giáo viên phụ trách")]
        public int? TeacherId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên lớp.")]
        [StringLength(100, ErrorMessage = "Tên lớp tối đa 100 ký tự.")]
        [Display(Name = "Tên lớp")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu.")]
        [Display(Name = "Ngày bắt đầu")]
        public DateOnly StartDate { get; set; }
            = DateOnly.FromDateTime(DateTime.Today);

        [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc.")]
        [Display(Name = "Ngày kết thúc")]
        public DateOnly EndDate { get; set; }
            = DateOnly.FromDateTime(DateTime.Today);

        [Range(1, 1000,
            ErrorMessage = "Sĩ số tối đa phải từ 1 đến 1000.")]
        [Display(Name = "Sĩ số tối đa")]
        public int MaxStudents { get; set; } = 20;

        [Range(typeof(decimal), "0", "9999999999999999",
            ErrorMessage = "Học phí không được âm.")]
        [Display(Name = "Học phí")]
        public decimal Fee { get; set; }

        [Display(Name = "Lớp tiên quyết")]
        public int? PrerequisiteClassId { get; set; }

        [Display(Name = "Trạng thái")]
        public ClassStatus Status { get; set; } = ClassStatus.Draft;

        // Mỗi phần tử gồm một thứ và một ca học riêng.
        public List<ClassScheduleInputViewModel> Schedules { get; set; }
            = new List<ClassScheduleInputViewModel>();

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (EndDate < StartDate)
            {
                yield return new ValidationResult(
                    "Ngày kết thúc phải bằng hoặc sau ngày bắt đầu.",
                    new[] { nameof(EndDate) });
            }

            if (Schedules == null || Schedules.Count == 0)
            {
                yield return new ValidationResult(
                    "Vui lòng thêm ít nhất một lịch học.",
                    new[] { nameof(Schedules) });

                yield break;
            }

            if (Schedules.Any(s => s == null))
            {
                yield return new ValidationResult(
                    "Có lịch học không hợp lệ.",
                    new[] { nameof(Schedules) });

                yield break;
            }

            if (Schedules.Any(s =>
                s.DayOfWeek < 2 || s.DayOfWeek > 8))
            {
                yield return new ValidationResult(
                    "Thứ học phải từ Thứ 2 đến Chủ nhật.",
                    new[] { nameof(Schedules) });
            }

            if (Schedules.Any(s => s.TimeSlotId <= 0))
            {
                yield return new ValidationResult(
                    "Vui lòng chọn ca học cho từng thứ.",
                    new[] { nameof(Schedules) });
            }

            if (Schedules.Select(s => s.DayOfWeek).Distinct().Count()
                != Schedules.Count)
            {
                yield return new ValidationResult(
                    "Mỗi thứ trong tuần chỉ được cấu hình một lần.",
                    new[] { nameof(Schedules) });
            }
        }
    }
}
