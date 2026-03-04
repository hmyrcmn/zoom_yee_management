using System;
using Toplanti.Core.Entities;

namespace Toplanti.Entities.DTOs.Auth
{
    public class AuthenticateLdapRequest : IDto
    {
        public string UsernameOrEmail { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
    }

    public class GenerateOtpRequest : IDto
    {
        public string Email { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
    }

    public class VerifyOtpRequest : IDto
    {
        public string Email { get; set; } = string.Empty;
        public string OtpCode { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
    }

    public abstract class ServiceResultBase : IDto
    {
        public bool Success { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class AuthenticationResult : ServiceResultBase
    {
        public Guid? UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsInternal { get; set; }
        public string SessionToken { get; set; } = string.Empty;
    }

    public class OtpGenerationResult : ServiceResultBase
    {
        public Guid? ChallengeId { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public int CooldownSecondsRemaining { get; set; }
    }

    public class OtpVerificationResult : ServiceResultBase
    {
        public Guid? UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
        public int RemainingAttempts { get; set; }
    }
}
