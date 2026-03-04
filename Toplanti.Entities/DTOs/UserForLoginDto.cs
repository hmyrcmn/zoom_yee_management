using Toplanti.Core.Entities;

namespace Toplanti.Entities.DTOs
{
    public class UserForLoginDto : IDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string OtpCode { get; set; } = string.Empty;
        public bool ForceOtpResend { get; set; }
    }
}
