
using EduCenterManagement.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduCenterManagement.Models
{
    [Table("payments")]
    public class Payment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int EnrollmentId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0.01", "9999999999999999")]
        public decimal Amount { get; set; }

        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

                [MaxLength(100)]
        public string? Gateway { get; set; }

        [MaxLength(100)]
        public string? PaymentReference { get; set; }

        [MaxLength(150)]
        public string? GatewayTransactionId { get; set; }

        [Required]
        [MaxLength(3)]
        public string Currency { get; set; } = "VND";

        [MaxLength(500)]
        public string? FailureReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? PaidAt { get; set; }

        public DateTime? ExpiresAt { get; set; }

        [ForeignKey(nameof(EnrollmentId))]
        public Enrollment Enrollment { get; set; } = null!;
    }
}
