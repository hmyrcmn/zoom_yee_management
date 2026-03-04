using Toplanti.Business.Abstract;

namespace Toplanti.IntegrationTests.TestHelpers
{
    internal sealed class OtpCaptureNotificationService : IAuthNotificationService
    {
        public string LastEmail { get; private set; } = string.Empty;
        public string LastCode { get; private set; } = string.Empty;
        public bool ShouldSucceed { get; set; } = true;

        public Task<bool> SendOtpCode(string email, string code)
        {
            LastEmail = email ?? string.Empty;
            LastCode = code ?? string.Empty;
            return Task.FromResult(ShouldSucceed);
        }
    }
}
