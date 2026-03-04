using Toplanti.Business.Abstract;
using Toplanti.Entities.DTOs.Auth;

namespace Toplanti.IntegrationTests.TestHelpers
{
    internal sealed class MockAuthenticationService : IAuthenticationService
    {
        public Func<AuthenticateLdapRequest, AuthenticationResult> AuthenticateLdapHandler { get; set; } =
            _ => new AuthenticationResult
            {
                Success = false,
                Code = "MOCK_NOT_CONFIGURED",
                Message = "Mock LDAP handler is not configured."
            };

        public Func<GenerateOtpRequest, OtpGenerationResult> GenerateOtpHandler { get; set; } =
            _ => new OtpGenerationResult
            {
                Success = false,
                Code = "MOCK_NOT_CONFIGURED",
                Message = "Mock OTP generation handler is not configured."
            };

        public Func<VerifyOtpRequest, OtpVerificationResult> VerifyOtpHandler { get; set; } =
            _ => new OtpVerificationResult
            {
                Success = false,
                Code = "MOCK_NOT_CONFIGURED",
                Message = "Mock OTP verification handler is not configured."
            };

        public Task<AuthenticationResult> AuthenticateLdapAsync(
            AuthenticateLdapRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AuthenticateLdapHandler(request));
        }

        public Task<OtpGenerationResult> GenerateOtpAsync(
            GenerateOtpRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GenerateOtpHandler(request));
        }

        public Task<OtpVerificationResult> VerifyOtpAsync(
            VerifyOtpRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(VerifyOtpHandler(request));
        }
    }
}
