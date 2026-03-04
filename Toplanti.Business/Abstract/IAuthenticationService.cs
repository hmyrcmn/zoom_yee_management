using System.Threading;
using System.Threading.Tasks;
using Toplanti.Entities.DTOs.Auth;

namespace Toplanti.Business.Abstract
{
    /// <summary>
    /// Provides enterprise authentication operations for LDAP and OTP based flows.
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>
        /// Authenticates an internal user against LDAP and auto-provisions the user into auth.Users when missing.
        /// </summary>
        /// <param name="request">LDAP authentication request payload.</param>
        /// <param name="cancellationToken">Cancellation token for async operation control.</param>
        /// <returns>Authentication result with user identity details.</returns>
        Task<AuthenticationResult> AuthenticateLdapAsync(
            AuthenticateLdapRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a secure OTP challenge for external authentication flow with cooldown protection.
        /// </summary>
        /// <param name="request">OTP generation request payload.</param>
        /// <param name="cancellationToken">Cancellation token for async operation control.</param>
        /// <returns>OTP generation result containing challenge metadata.</returns>
        Task<OtpGenerationResult> GenerateOtpAsync(
            GenerateOtpRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies an OTP challenge and consumes it on successful validation.
        /// </summary>
        /// <param name="request">OTP verification request payload.</param>
        /// <param name="cancellationToken">Cancellation token for async operation control.</param>
        /// <returns>OTP verification result with lock and attempt metadata.</returns>
        Task<OtpVerificationResult> VerifyOtpAsync(
            VerifyOtpRequest request,
            CancellationToken cancellationToken = default);
    }
}
