
using EduCenterManagement.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduCenterManagement.Models
{
    [Table("attendance_records")]
    public class AttendanceRecord
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SessionId { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Required]
        public int MarkedByTeacherId { get; set; }

        public AttendanceStatus Status { get; set; } = AttendanceStatus.Absent;

        [MaxLength(500)]
        public string? Note { get; set; }

        public DateTime MarkedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(SessionId))]
        public ClassSession Session { get; set; } = null!;

        [ForeignKey(nameof(StudentId))]
        public StudentProfile Student { get; set; } = null!;

        [ForeignKey(nameof(MarkedByTeacherId))]
        public TeacherProfile MarkedByTeacher { get; set; } = null!;
    }
}
