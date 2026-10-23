
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduCenterManagement.Models
{
    [Table("class_schedules")]
    public class ClassSchedule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClassId { get; set; }

        [Required]
        public int TimeSlotId { get; set; }

        // 1 = Monday, ..., 7 = Sunday
        [Range(1, 7)]
        public int DayOfWeek { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(ClassId))]
        public Class Class { get; set; } = null!;

        [ForeignKey(nameof(TimeSlotId))]
        public TimeSlot TimeSlot { get; set; } = null!;
    }
}
