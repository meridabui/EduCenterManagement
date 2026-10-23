
using EduCenterManagement.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduCenterManagement.Models
{
    [Table("email_outbox")]
    public class EmailOutbox
    {
        [Key]
        public int Id { get; set; }

        public int? RecipientUserId { get; set; }

        [Required]
        [MaxLength(256)]
        public string ToEmail { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string TemplateKey { get; set; } = string.Empty;

        public string? PayloadJson { get; set; }

        public DateTime ScheduledAt { get; set; } = DateTime.UtcNow;

        public DateTime? SentAt { get; set; }

        public EmailOutboxStatus Status { get; set; } = EmailOutboxStatus.Pending;

        public int RetryCount { get; set; } = 0;

        [MaxLength(1000)]
        public string? LastError { get; set; }

        [MaxLength(150)]
        public string? DeduplicationKey { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(RecipientUserId))]
        public AppUser? RecipientUser { get; set; }
    }
}
