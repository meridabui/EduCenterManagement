
using EduCenterManagement.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduCenterManagement.Models
{
    [Table("class_sessions")]
    public class ClassSession
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClassId { get; set; }

        public int? ClassScheduleId { get; set; }

        [Required]
        public DateOnly SessionDate { get; set; }

        [Required]
        public TimeOnly StartsAt { get; set; }

        [Required]
        public TimeOnly EndsAt { get; set; }

        [Range(1, int.MaxValue)]
        public int SessionNumber { get; set; }

        public ClassSessionStatus Status { get; set; } = ClassSessionStatus.Scheduled;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(ClassId))]
        public Class Class { get; set; } = null!;

        [ForeignKey(nameof(ClassScheduleId))]
        public ClassSchedule? ClassSchedule { get; set; }
    }
}
