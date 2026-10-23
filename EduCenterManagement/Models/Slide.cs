
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduCenterManagement.Models
{
    [Table("slides")]
    public class Slide
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SessionId { get; set; }

        [Required]
        public int UploadedByTeacherId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string FileUrl { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? OriginalFileName { get; set; }

        [MaxLength(100)]
        public string? StorageKey { get; set; }

        [MaxLength(100)]
        public string? MimeType { get; set; }

        public long? FileSize { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(SessionId))]
        public ClassSession Session { get; set; } = null!;

        [ForeignKey(nameof(UploadedByTeacherId))]
        public TeacherProfile UploadedByTeacher { get; set; } = null!;
    }
}
