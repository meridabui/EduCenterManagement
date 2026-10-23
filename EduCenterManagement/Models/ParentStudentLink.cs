
using EduCenterManagement.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduCenterManagement.Models
{
    [Table("parent_student_links")]
    public class ParentStudentLink
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ParentId { get; set; }

        [Required]
        public int StudentId { get; set; }

        public ParentStudentLinkStatus Status { get; set; } = ParentStudentLinkStatus.Pending;

        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RespondedAt { get; set; }

        [ForeignKey(nameof(ParentId))]
        public ParentProfile Parent { get; set; } = null!;

        [ForeignKey(nameof(StudentId))]
        public StudentProfile Student { get; set; } = null!;
    }
}
