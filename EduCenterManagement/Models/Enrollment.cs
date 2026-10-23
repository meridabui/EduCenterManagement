
using EduCenterManagement.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduCenterManagement.Models
{
    [Table("enrollments")]
    public class Enrollment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Required]
        public int ClassId { get; set; }

        [Required]
        public int RegisteredByParentId { get; set; }

        public EnrollmentStatus Status { get; set; } = EnrollmentStatus.PendingPayment;
        public DateTime? ReservedUntil { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ConfirmedAt { get; set; }

        public DateTime? CancelledAt { get; set; }

        [ForeignKey(nameof(StudentId))]
        public StudentProfile Student { get; set; } = null!;

        [ForeignKey(nameof(ClassId))]
        public Class Class { get; set; } = null!;

        [ForeignKey(nameof(RegisteredByParentId))]
        public ParentProfile RegisteredByParent { get; set; } = null!;
    }
}
