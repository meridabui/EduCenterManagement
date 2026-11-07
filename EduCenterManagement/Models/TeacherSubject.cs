using System.ComponentModel.DataAnnotations.Schema;

namespace EduCenterManagement.Models
{
    [Table("teacher_subjects")]
    public class TeacherSubject
    {
        public int TeacherId { get; set; }
        public int SubjectId { get; set; }

        public TeacherProfile Teacher { get; set; } = null!;
        public Subject Subject { get; set; } = null!;
    }
}
