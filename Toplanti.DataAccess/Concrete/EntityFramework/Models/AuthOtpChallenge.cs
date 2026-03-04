using System;

namespace Toplanti.DataAccess.Concrete.EntityFramework.Models
{
    public class AuthOtpChallenge
    {
        public Guid OtpChallengeId { get; set; }
        public Guid? UserId { get; set; }
        public string EmailNormalized { get; set; } = string.Empty;
        public byte Purpose { get; set; }
        public byte[] OtpCodeHash { get; set; } = Array.Empty<byte>();
        public byte[] OtpCodeSalt { get; set; } = Array.Empty<byte>();
        public short AttemptCount { get; set; }
        public short MaxAttempts { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? ConsumedAt { get; set; }
        public byte DeliveryChannel { get; set; }
        public string? RequestIpAddress { get; set; }
        public Guid? CorrelationId { get; set; }
        public DateTime CreatedAt { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public AuthUser? User { get; set; }
    }
}
