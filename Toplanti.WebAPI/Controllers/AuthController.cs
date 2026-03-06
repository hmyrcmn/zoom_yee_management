using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Toplanti.Business.Abstract;
using Toplanti.Business.Constants;
using Toplanti.DataAccess.Concrete.EntityFramework.Contexts;
using Toplanti.DataAccess.Concrete.EntityFramework.Models;
using Toplanti.Entities.DTOs.Auth;
using Toplanti.Entities.DTOs.ZoomProvisioning;
using Toplanti.Entities.Enums;

namespace Toplanti.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class AuthController : ControllerBase
    {
        private const string UiActionDashboard = "MeetingDashboard";
        private const string UiActionActivationWarning = "ActivationWarning";
        private const string UiActionContactIt = "ITSupportButton";

        private readonly IAuthenticationService _authenticationService;
        private readonly IZoomProvisioningService _zoomProvisioningService;
        private readonly ToplantiContext _context;
        private readonly IConfiguration _configuration;
        private readonly string _corporateDomain;
        private readonly int _activationPendingEscalationMinutes;
        private readonly int _activationPendingEscalationReminderCount;

        public AuthController(
            IAuthenticationService authenticationService,
            IZoomProvisioningService zoomProvisioningService,
            ToplantiContext context,
            IConfiguration configuration)
        {
            _authenticationService = authenticationService;
            _zoomProvisioningService = zoomProvisioningService;
            _context = context;
            _configuration = configuration;
            _corporateDomain = (_configuration["AuthFlow:CorporateDomain"] ?? "yee.org.tr")
                .Trim()
                .ToLowerInvariant();
            _activationPendingEscalationMinutes = Math.Max(
                5,
                _configuration.GetValue<int?>("AuthFlow:ActivationPendingEscalationMinutes") ?? 30);
            _activationPendingEscalationReminderCount = Math.Max(
                1,
                _configuration.GetValue<int?>("AuthFlow:ActivationPendingEscalationReminderCount") ?? 3);
        }

        [HttpPost("generate-otp")]
        public async Task<ActionResult> GenerateOtp(
            [FromBody] GenerateOtpCommand? request,
            CancellationToken cancellationToken)
        {
            var email = (request?.Email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                return Ok(new
                {
                    Success = false,
                    ErrorCode = AuthenticationResultCodes.InvalidRequest,
                    Message = "Email is required."
                });
            }

            if (IsCorporateEmail(email))
            {
                return Ok(new
                {
                    Success = false,
                    ErrorCode = AuthenticationResultCodes.InvalidRequest,
                    Message = "Corporate users must login with LDAP credentials."
                });
            }

            var checkResult = await _zoomProvisioningService.CheckAccountStatusAsync(
                new CheckZoomAccountStatusRequest
                {
                    Email = email,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                    ForceRefresh = true
                },
                cancellationToken);

            if (!checkResult.Success)
            {
                return Ok(new
                {
                    Success = false,
                    ErrorCode = checkResult.Code,
                    Message = string.IsNullOrWhiteSpace(checkResult.Message)
                        ? "Zoom hesap durumu kontrol edilemedi."
                        : checkResult.Message
                });
            }

            var zoomStatus = NormalizeStatusName(checkResult.StatusName);
            var hasZoomAccount = string.Equals(
                                     zoomStatus,
                                     ZoomProvisioningStatus.Active.ToString(),
                                     StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(
                                     zoomStatus,
                                     ZoomProvisioningStatus.ActivationPending.ToString(),
                                     StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(
                                     zoomStatus,
                                     ZoomProvisioningStatus.ProvisioningPending.ToString(),
                                     StringComparison.OrdinalIgnoreCase);

            if (!hasZoomAccount)
            {
                return Ok(new
                {
                    Success = false,
                    ErrorCode = "ZOOM_ACCOUNT_REQUIRED",
                    Message = "Bu e-posta adresi icin Zoom hesabi bulunamadi. Kurumsal olmayan kullanicilar uygulamaya giris yapamaz. Lutfen once Zoom hesabi olusturun.",
                    Data = new
                    {
                        ZoomStatus = zoomStatus,
                        ZoomStatusCode = checkResult.Code,
                        ZoomUiAction = UiActionContactIt
                    }
                });
            }

            var generationResult = await _authenticationService.GenerateOtpAsync(
                new GenerateOtpRequest
                {
                    Email = email,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty
                },
                cancellationToken);

            if (!generationResult.Success)
            {
                var flowCode = generationResult.Code == AuthenticationResultCodes.OtpDeliveryFailed
                    ? AuthFlowCodes.OtpDeliveryFailed
                    : AuthFlowCodes.OtpRequired;

                return Ok(new
                {
                    Success = false,
                    ErrorCode = flowCode,
                    Message = generationResult.Message,
                    Data = new
                    {
                        generationResult.ExpiresAtUtc,
                        generationResult.CooldownSecondsRemaining
                    }
                });
            }

            return Ok(new
            {
                Success = true,
                ErrorCode = string.Empty,
                Message = generationResult.Message,
                Data = new
                {
                    generationResult.ChallengeId,
                    generationResult.ExpiresAtUtc,
                    generationResult.CooldownSecondsRemaining
                }
            });
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login(
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken)
        {
            var identity = ResolveIdentity(request);
            var requestEmail = (request?.Email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(identity))
            {
                return BadRequest(new
                {
                    Success = false,
                    ErrorCode = AuthenticationResultCodes.InvalidRequest,
                    Message = "Username/email is required."
                });
            }

            var isCorporateLogin = IsCorporateEmail(identity) || IsCorporateEmail(requestEmail);
            if (!isCorporateLogin)
            {
                return await HandleExternalLoginAsync(request, identity, cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(request?.Password))
            {
                return BadRequest(new
                {
                    Success = false,
                    ErrorCode = AuthenticationResultCodes.InvalidRequest,
                    Message = "Password is required for corporate login."
                });
            }

            var corporateIdentity = IsCorporateEmail(identity)
                ? identity
                : requestEmail;

            var ldapResult = await _authenticationService.AuthenticateLdapAsync(
                new AuthenticateLdapRequest
                {
                    UsernameOrEmail = corporateIdentity,
                    Password = request.Password,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                    UserAgent = Request.Headers.UserAgent.ToString()
                },
                cancellationToken);

            if (!ldapResult.Success || !ldapResult.UserId.HasValue)
            {
                return Unauthorized(new
                {
                    Success = false,
                    ErrorCode = ldapResult.Code,
                    Message = ldapResult.Message
                });
            }

            var department = await ResolveDepartmentAsync(ldapResult.UserId.Value, ldapResult.Email, cancellationToken);
            return await BuildAuthenticatedResponseAsync(
                ldapResult.UserId.Value,
                ldapResult.Email,
                department,
                isInternal: true,
                successMessage: "Login successful.",
                cancellationToken);
        }

        [HttpPost("verify-otp")]
        public async Task<ActionResult> VerifyOtp(
            [FromBody] VerifyOtpRequest? request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return Ok(new
                {
                    Success = false,
                    ErrorCode = AuthenticationResultCodes.InvalidRequest,
                    Message = "Request payload is required."
                });
            }

            var email = (request.Email ?? string.Empty).Trim();
            var otpCodeRaw = (request.OtpCode ?? string.Empty).Trim();
            var otpCode = new string(otpCodeRaw.Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otpCode))
            {
                return Ok(new
                {
                    Success = false,
                    ErrorCode = AuthenticationResultCodes.InvalidRequest,
                    Message = "Email and otpCode are required."
                });
            }

            if (IsCorporateEmail(email))
            {
                return Ok(new
                {
                    Success = false,
                    ErrorCode = AuthenticationResultCodes.InvalidRequest,
                    Message = "Corporate users must login with LDAP credentials."
                });
            }

            request.Email = email;
            request.OtpCode = otpCode;
            request.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            var otpResult = await _authenticationService.VerifyOtpAsync(request, cancellationToken);

            if (!otpResult.Success || !otpResult.UserId.HasValue)
            {
                return Ok(new
                {
                    Success = false,
                    ErrorCode = MapOtpVerificationErrorCode(otpResult.Code),
                    Message = otpResult.Message,
                    IsLocked = otpResult.IsLocked,
                    RemainingAttempts = otpResult.RemainingAttempts
                });
            }

            var department = await ResolveDepartmentAsync(otpResult.UserId.Value, otpResult.Email, cancellationToken);
            var isCorporateUser = IsCorporateEmail(otpResult.Email);
            if (!isCorporateUser)
            {
                var checkResult = await _zoomProvisioningService.CheckAccountStatusAsync(
                    new CheckZoomAccountStatusRequest
                    {
                        Email = otpResult.Email,
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                        ForceRefresh = true
                    },
                    cancellationToken);

                if (!checkResult.Success)
                {
                    return Ok(new
                    {
                        Success = false,
                        ErrorCode = checkResult.Code,
                        Message = string.IsNullOrWhiteSpace(checkResult.Message)
                            ? "Zoom hesap durumu kontrol edilemedi."
                            : checkResult.Message
                    });
                }

                var zoomStatus = NormalizeStatusName(checkResult.StatusName);
                var hasZoomAccount = string.Equals(
                                         zoomStatus,
                                         ZoomProvisioningStatus.Active.ToString(),
                                         StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(
                                         zoomStatus,
                                         ZoomProvisioningStatus.ActivationPending.ToString(),
                                         StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(
                                         zoomStatus,
                                         ZoomProvisioningStatus.ProvisioningPending.ToString(),
                                         StringComparison.OrdinalIgnoreCase);

                if (!hasZoomAccount)
                {
                    return Ok(new
                    {
                        Success = false,
                        ErrorCode = "ZOOM_ACCOUNT_REQUIRED",
                        Message = "Bu e-posta adresi icin Zoom hesabi bulunamadi. Kurumsal olmayan kullanicilar uygulamaya giris yapamaz. Lutfen once Zoom hesabi olusturun.",
                        Data = new
                        {
                            ZoomStatus = zoomStatus,
                            ZoomStatusCode = checkResult.Code,
                            ZoomUiAction = UiActionContactIt
                        }
                    });
                }
            }

            return await BuildAuthenticatedResponseAsync(
                otpResult.UserId.Value,
                otpResult.Email,
                department,
                isCorporateUser,
                "OTP verification successful.",
                cancellationToken,
                forceDashboardForExternal: !isCorporateUser);
        }

        [HttpGet("zoom-activation-status")]
        public async Task<ActionResult> GetZoomActivationStatus(
            [FromQuery] string email,
            [FromQuery] bool forceResend,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new
                {
                    Success = false,
                    ErrorCode = AuthenticationResultCodes.InvalidRequest,
                    Message = "Email is required."
                });
            }

            var checkResult = await _zoomProvisioningService.CheckAccountStatusAsync(
                new CheckZoomAccountStatusRequest
                {
                    Email = email.Trim(),
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                    ForceRefresh = true,
                    ForceActivationInviteResend = forceResend
                },
                cancellationToken);

            if (!checkResult.Success)
            {
                return BadRequest(new
                {
                    Success = false,
                    ErrorCode = checkResult.Code,
                    Message = checkResult.Message
                });
            }

            var zoomStatus = NormalizeStatusName(checkResult.StatusName);
            var active = string.Equals(
                zoomStatus,
                ZoomProvisioningStatus.Active.ToString(),
                StringComparison.OrdinalIgnoreCase);

            var activationPending =
                string.Equals(zoomStatus, ZoomProvisioningStatus.ActivationPending.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(zoomStatus, ZoomProvisioningStatus.ProvisioningPending.ToString(), StringComparison.OrdinalIgnoreCase);
            var shouldEscalateToIt = activationPending
                && !active
                && await ShouldEscalateActivationPendingToItAsync(email, cancellationToken);
            var uiAction = shouldEscalateToIt
                ? UiActionContactIt
                : ResolveZoomUiAction(zoomStatus);

            return Ok(new
            {
                Success = active && !shouldEscalateToIt,
                ErrorCode = shouldEscalateToIt ? AuthFlowCodes.BilisimContactRequired : string.Empty,
                Message = shouldEscalateToIt
                    ? "Zoom activation email could not be confirmed after multiple reminders. Please contact IT support."
                    : (active
                        ? "Zoom account is active."
                        : (string.IsNullOrWhiteSpace(checkResult.Message)
                            ? "Zoom account activation is still pending."
                            : checkResult.Message)),
                IsActive = active,
                Data = active,
                ZoomStatus = zoomStatus,
                ZoomUiAction = uiAction
            });
        }

        [HttpGet("userinfo")]
        public ActionResult GetUserInfo()
        {
            if (User?.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Unauthorized(new
                {
                    Success = false,
                    ErrorCode = "UNAUTHORIZED",
                    Message = "Authentication is required."
                });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? string.Empty;
            var email = User.FindFirstValue(ClaimTypes.Email)
                ?? User.FindFirstValue("email")
                ?? string.Empty;
            var department = User.FindFirstValue("department") ?? string.Empty;
            var roles = User.FindAll(ClaimTypes.Role)
                .Select(claim => claim.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Ok(new
            {
                Success = true,
                Message = "User info loaded.",
                Data = new
                {
                    UserId = userId,
                    Email = email,
                    Department = department,
                    Roles = roles
                }
            });
        }

        private static string ResolveIdentity(LoginRequest? request)
        {
            if (!string.IsNullOrWhiteSpace(request?.UsernameOrEmail))
            {
                return request!.UsernameOrEmail.Trim();
            }

            return request?.Email?.Trim() ?? string.Empty;
        }

        private async Task<ActionResult> HandleExternalLoginAsync(
            LoginRequest request,
            string identity,
            CancellationToken cancellationToken)
        {
            var email = (request.Email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(email) && identity.Contains("@", StringComparison.Ordinal))
            {
                email = identity.Trim();
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new
                {
                    Success = false,
                    ErrorCode = AuthenticationResultCodes.InvalidRequest,
                    Message = "External login requires a valid email address."
                });
            }

            var checkResult = await _zoomProvisioningService.CheckAccountStatusAsync(
                new CheckZoomAccountStatusRequest
                {
                    Email = email,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                    ForceRefresh = true
                },
                cancellationToken);

            if (!checkResult.Success)
            {
                return Ok(new
                {
                    Success = false,
                    ErrorCode = checkResult.Code,
                    Message = string.IsNullOrWhiteSpace(checkResult.Message)
                        ? "Zoom hesap durumu kontrol edilemedi."
                        : checkResult.Message
                });
            }

            var zoomStatus = NormalizeStatusName(checkResult.StatusName);
            var active = string.Equals(
                zoomStatus,
                ZoomProvisioningStatus.Active.ToString(),
                StringComparison.OrdinalIgnoreCase);
            var activationPending =
                string.Equals(zoomStatus, ZoomProvisioningStatus.ActivationPending.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(zoomStatus, ZoomProvisioningStatus.ProvisioningPending.ToString(), StringComparison.OrdinalIgnoreCase);
            var hasZoomAccount = active || activationPending;
            if (!hasZoomAccount)
            {
                var externalLoginBlockedMessage = string.Equals(
                    zoomStatus,
                    ZoomProvisioningStatus.None.ToString(),
                    StringComparison.OrdinalIgnoreCase)
                    ? "Bu e-posta adresi icin Zoom hesabi bulunamadi. Kurumsal olmayan kullanicilar uygulamaya giris yapamaz. Lutfen once Zoom hesabi olusturun."
                    : (string.IsNullOrWhiteSpace(checkResult.Message)
                        ? "Uygulamaya giris icin aktif bir Zoom hesabi gereklidir."
                        : checkResult.Message);

                return Ok(new
                {
                    Success = false,
                    ErrorCode = "ZOOM_ACCOUNT_REQUIRED",
                    Message = externalLoginBlockedMessage,
                    Data = new
                    {
                        ZoomStatus = zoomStatus,
                        ZoomStatusCode = checkResult.Code,
                        ZoomUiAction = UiActionContactIt
                    }
                });
            }

            var otpCode = new string((request.OtpCode ?? string.Empty).Where(char.IsDigit).ToArray());
            if (request.ForceOtpResend || string.IsNullOrWhiteSpace(otpCode))
            {
                var generationResult = await _authenticationService.GenerateOtpAsync(
                    new GenerateOtpRequest
                    {
                        Email = email,
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty
                    },
                    cancellationToken);

                if (!generationResult.Success)
                {
                    var flowCode = generationResult.Code == AuthenticationResultCodes.OtpDeliveryFailed
                        ? AuthFlowCodes.OtpDeliveryFailed
                        : AuthFlowCodes.OtpRequired;

                    return Ok(new
                    {
                        Success = false,
                        ErrorCode = flowCode,
                        Message = generationResult.Message,
                        Data = new
                        {
                            generationResult.ExpiresAtUtc,
                            generationResult.CooldownSecondsRemaining
                        }
                    });
                }

                return Ok(new
                {
                    Success = false,
                    ErrorCode = AuthFlowCodes.OtpRequired,
                    Message = "Zoom hesabi dogrulandi. Girisi tamamlamak icin OTP kodu e-posta adresinize gonderildi.",
                    Data = new
                    {
                        generationResult.ExpiresAtUtc,
                        generationResult.CooldownSecondsRemaining
                    }
                });
            }

            var otpResult = await _authenticationService.VerifyOtpAsync(
                new VerifyOtpRequest
                {
                    Email = email,
                    OtpCode = otpCode,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty
                },
                cancellationToken);

            if (!otpResult.Success || !otpResult.UserId.HasValue)
            {
                return Ok(new
                {
                    Success = false,
                    ErrorCode = MapOtpVerificationErrorCode(otpResult.Code),
                    Message = otpResult.Message,
                    IsLocked = otpResult.IsLocked,
                    RemainingAttempts = otpResult.RemainingAttempts
                });
            }

            var department = await ResolveDepartmentAsync(otpResult.UserId.Value, otpResult.Email, cancellationToken);
            return await BuildAuthenticatedResponseAsync(
                otpResult.UserId.Value,
                otpResult.Email,
                department,
                isInternal: false,
                successMessage: "OTP verification successful.",
                cancellationToken,
                forceDashboardForExternal: true);
        }

        private async Task<ActionResult> BuildAuthenticatedResponseAsync(
            Guid userId,
            string email,
            string department,
            bool isInternal,
            string successMessage,
            CancellationToken cancellationToken,
            bool forceDashboardForExternal = false)
        {
            var requiredStatusIds = Enum
                .GetValues<ZoomProvisioningStatus>()
                .Select(status => (byte)status)
                .ToArray();

            var seededStatusIds = await _context.ZoomStatuses
                .AsNoTracking()
                .Where(status => requiredStatusIds.Contains(status.ZoomStatusId))
                .Select(status => status.ZoomStatusId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var statusCatalogSeeded = seededStatusIds.Count == requiredStatusIds.Length;

            if (!statusCatalogSeeded)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    ErrorCode = "DB_SEED_MISSING",
                    Message = "System Configuration Error: Zoom Statuses not seeded."
                });
            }

            var zoomOutcome = await ResolveZoomLoginOutcomeAsync(
                userId,
                email,
                department,
                cancellationToken);

            if (!zoomOutcome.Success)
            {
                return Ok(new
                {
                    Success = false,
                    ErrorCode = zoomOutcome.ErrorCode,
                    Message = zoomOutcome.Message,
                    Data = new
                    {
                        ZoomStatus = zoomOutcome.ZoomStatus,
                        ZoomStatusCode = zoomOutcome.ZoomStatusCode,
                        ZoomUiAction = zoomOutcome.UiAction
                    }
                });
            }

            var responseZoomStatus = zoomOutcome.ZoomStatus;
            var responseZoomStatusCode = zoomOutcome.ZoomStatusCode;
            var responseZoomUiAction = zoomOutcome.UiAction;

            if (forceDashboardForExternal && !isInternal)
            {
                responseZoomStatus = ZoomProvisioningStatus.Active.ToString();
                responseZoomUiAction = UiActionDashboard;
            }

            if (!string.Equals(responseZoomUiAction, UiActionDashboard, StringComparison.Ordinal))
            {
                var activationWarning = string.Equals(
                    responseZoomUiAction,
                    UiActionActivationWarning,
                    StringComparison.Ordinal);
                var shouldEscalateToIt = activationWarning
                    && await ShouldEscalateActivationPendingToItAsync(email, cancellationToken);

                var flowCode = activationWarning && !shouldEscalateToIt
                    ? AuthFlowCodes.ZoomActivationPending
                    : AuthFlowCodes.BilisimContactRequired;

                var flowMessage = shouldEscalateToIt
                    ? "Zoom activation email could not be confirmed after multiple reminders. Please contact IT support."
                    : (string.IsNullOrWhiteSpace(zoomOutcome.Message)
                        ? ResolveFlowMessage(zoomOutcome.ZoomStatus)
                        : zoomOutcome.Message);

                return Ok(new
                {
                    Success = false,
                    ErrorCode = flowCode,
                    Message = flowMessage,
                    Data = new
                    {
                        UserId = userId,
                        Email = email,
                        Department = department,
                        ZoomStatus = responseZoomStatus,
                        ZoomStatusCode = responseZoomStatusCode,
                        ZoomUiAction = responseZoomUiAction
                    }
                });
            }

            var tokenResult = GenerateJwtToken(userId, email, department, isInternal);
            return Ok(new
            {
                Success = true,
                ErrorCode = string.Empty,
                Message = successMessage,
                Data = new
                {
                    UserId = userId,
                    Email = email,
                    Department = department,
                    Token = tokenResult.Token,
                    ExpiresAtUtc = tokenResult.ExpiresAtUtc,
                    ZoomStatus = responseZoomStatus,
                    ZoomStatusCode = responseZoomStatusCode,
                    ZoomUiAction = responseZoomUiAction
                }
            });
        }

        private async Task<ZoomLoginOutcome> ResolveZoomLoginOutcomeAsync(
            Guid userId,
            string email,
            string department,
            CancellationToken cancellationToken)
        {
            var checkResult = await _zoomProvisioningService.CheckAccountStatusAsync(
                new CheckZoomAccountStatusRequest
                {
                    Email = email,
                    ActorUserId = userId,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty
                },
                cancellationToken);

            if (!checkResult.Success)
            {
                return ZoomLoginOutcome.Fail(
                    AuthFlowCodes.ZoomValidationFailed,
                    checkResult.Message,
                    ZoomProvisioningStatus.Failed.ToString(),
                    checkResult.Code,
                    UiActionContactIt);
            }

            var zoomStatus = NormalizeStatusName(checkResult.StatusName);
            var zoomStatusCode = checkResult.Code;
            var zoomMessage = checkResult.Message;

            if (ShouldAttemptAutoProvision(email, zoomStatus))
            {
                var (firstName, lastName) = await ResolveProvisionIdentityAsync(userId, email, cancellationToken);
                var provisionResult = await _zoomProvisioningService.ProvisionUserAsync(
                    new ProvisionZoomUserRequest
                    {
                        Email = email,
                        FirstName = firstName,
                        LastName = lastName,
                        UserType = ResolveZoomLicenseType(department, email),
                        ActorUserId = userId,
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty
                    },
                    cancellationToken);

                if (!provisionResult.Success)
                {
                    return ZoomLoginOutcome.Fail(
                        AuthFlowCodes.ZoomAutoProvisionFailed,
                        "Zoom account could not be auto-provisioned. Please contact IT support.",
                        NormalizeStatusName(provisionResult.StatusName),
                        provisionResult.Code,
                        UiActionContactIt);
                }

                zoomStatus = NormalizeStatusName(provisionResult.StatusName);
                zoomStatusCode = provisionResult.Code;
                zoomMessage = provisionResult.Message;
            }

            return ZoomLoginOutcome.Ok(
                zoomStatus,
                zoomStatusCode,
                ResolveZoomUiAction(zoomStatus),
                zoomMessage);
        }

        private async Task<string> ResolveDepartmentAsync(Guid userId, string email, CancellationToken cancellationToken)
        {
            var user = await _context.AuthUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            if (user == null && !string.IsNullOrWhiteSpace(email))
            {
                var emailKey = email.Trim().ToUpperInvariant();
                user = await _context.AuthUsers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.EmailNormalized == emailKey, cancellationToken);
            }

            return user?.Department ?? string.Empty;
        }

        private async Task<(string FirstName, string LastName)> ResolveProvisionIdentityAsync(
            Guid userId,
            string email,
            CancellationToken cancellationToken)
        {
            var user = await _context.AuthUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            if (!string.IsNullOrWhiteSpace(user?.DisplayName))
            {
                var parts = user.DisplayName
                    .Trim()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    return (parts[0], string.Join(" ", parts.Skip(1)));
                }

                if (parts.Length == 1)
                {
                    return (parts[0], "User");
                }
            }

            var localPart = (email ?? string.Empty).Split('@').FirstOrDefault() ?? string.Empty;
            var normalizedParts = localPart
                .Split(new[] { '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(TitleCaseInvariant)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (normalizedParts.Count >= 2)
            {
                return (normalizedParts[0], string.Join(" ", normalizedParts.Skip(1)));
            }

            if (normalizedParts.Count == 1)
            {
                return (normalizedParts[0], "User");
            }

            return ("User", "Account");
        }

        private TokenResponse GenerateJwtToken(
            Guid userId,
            string email,
            string department,
            bool isInternal)
        {
            var issuer = _configuration["TokenOptions:Issuer"] ?? "yee-toplanti";
            var audience = _configuration["TokenOptions:Audience"] ?? "yee-toplanti";
            var securityKeyRaw = _configuration["TokenOptions:SecurityKey"];
            if (string.IsNullOrWhiteSpace(securityKeyRaw))
            {
                throw new InvalidOperationException("TokenOptions:SecurityKey is not configured.");
            }

            var expiryMinutes = Math.Max(
                5,
                _configuration.GetValue<int?>("TokenOptions:AccessTokenExpiration") ?? 60);

            var now = DateTime.UtcNow;
            var expiresAt = now.AddMinutes(expiryMinutes);
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(securityKeyRaw));
            var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, email ?? string.Empty),
                new Claim("email", email ?? string.Empty),
                new Claim("department", department ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant())
            };

            foreach (var role in ResolveRoles(department ?? string.Empty, isInternal))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var tokenDescriptor = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: now,
                expires: expiresAt,
                signingCredentials: signingCredentials);

            var token = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
            return new TokenResponse(token, expiresAt);
        }

        private static string MapOtpVerificationErrorCode(string code)
        {
            return code switch
            {
                AuthenticationResultCodes.OtpInvalid => AuthFlowCodes.OtpInvalid,
                AuthenticationResultCodes.OtpExpiredOrNotFound => AuthFlowCodes.OtpRequired,
                AuthenticationResultCodes.OtpLocked => AuthFlowCodes.OtpInvalid,
                _ => code
            };
        }

        private bool ShouldAttemptAutoProvision(string email, string zoomStatus)
        {
            return IsCorporateEmail(email)
                   && string.Equals(
                       zoomStatus,
                       ZoomProvisioningStatus.None.ToString(),
                       StringComparison.OrdinalIgnoreCase);
        }

        private int ResolveZoomLicenseType(string department, string email)
        {
            var corporateType = _configuration.GetValue<int?>("AuthFlow:CorporateUserType") ?? 1;
            var bilisimType = _configuration.GetValue<int?>("AuthFlow:BilisimUserType") ?? 2;
            var externalType = _configuration.GetValue<int?>("AuthFlow:ExternalUserType") ?? corporateType;

            if (!IsCorporateEmail(email))
            {
                return externalType;
            }

            if (IsBilisimDepartment(department))
            {
                return bilisimType;
            }

            return corporateType;
        }

        private static string NormalizeStatusName(string? statusName)
        {
            if (Enum.TryParse<ZoomProvisioningStatus>(
                    (statusName ?? string.Empty).Trim(),
                    ignoreCase: true,
                    out var parsed))
            {
                return parsed.ToString();
            }

            return string.IsNullOrWhiteSpace(statusName)
                ? ZoomProvisioningStatus.None.ToString()
                : statusName.Trim();
        }

        private static string ResolveZoomUiAction(string zoomStatus)
        {
            if (string.Equals(zoomStatus, ZoomProvisioningStatus.Active.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return UiActionDashboard;
            }

            if (string.Equals(zoomStatus, ZoomProvisioningStatus.ActivationPending.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(zoomStatus, ZoomProvisioningStatus.ProvisioningPending.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return UiActionActivationWarning;
            }

            return UiActionContactIt;
        }

        private static string ResolveFlowMessage(string zoomStatus)
        {
            if (string.Equals(zoomStatus, ZoomProvisioningStatus.ActivationPending.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(zoomStatus, ZoomProvisioningStatus.ProvisioningPending.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return "Zoom account exists but activation is still pending. Please approve the Zoom invitation email. If you already have a Zoom account, accept the account-join invitation.";
            }

            return "Zoom account is not ready. Please contact IT support.";
        }

        private async Task<bool> ShouldEscalateActivationPendingToItAsync(
            string email,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            var emailKey = email.Trim().ToUpperInvariant();
            var provisioning = await _context.ZoomUserProvisionings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.EmailNormalized == emailKey, cancellationToken);

            if (provisioning == null
                || provisioning.ZoomStatusId != (byte)ZoomProvisioningStatus.ActivationPending)
            {
                return false;
            }

            var ageMinutes = (DateTime.UtcNow - provisioning.CreatedAt).TotalMinutes;
            if (ageMinutes < _activationPendingEscalationMinutes)
            {
                return false;
            }

            const string activationInviteResendAction = "ACTIVATION_INVITE_RESEND";
            var reminderCount = await _context.ZoomUserProvisioningHistories
                .AsNoTracking()
                .CountAsync(
                    x => x.UserProvisioningId == provisioning.UserProvisioningId
                         && x.ActionType == activationInviteResendAction,
                    cancellationToken);

            return reminderCount >= _activationPendingEscalationReminderCount;
        }

        private IEnumerable<string> ResolveRoles(string department, bool isInternal)
        {
            if (!isInternal)
            {
                return new[] { "ExternalUser" };
            }

            if (IsBilisimDepartment(department))
            {
                return new[] { "Admin", "IT", "User" };
            }

            return new[] { "User" };
        }

        private bool IsCorporateEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(_corporateDomain))
            {
                return false;
            }

            if (!email.Contains("@", StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                var parsed = new System.Net.Mail.MailAddress(email.Trim());
                return parsed.Address.EndsWith("@" + _corporateDomain, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsBilisimDepartment(string? department)
        {
            if (string.IsNullOrWhiteSpace(department))
            {
                return false;
            }

            var normalized = department
                .Trim()
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormD);

            var chars = normalized
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .Select(c => c == (char)0x0131 ? 'i' : c)
                .ToArray();

            return new string(chars) == "bilisim";
        }

        private static string TitleCaseInvariant(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var trimmed = input.Trim().ToLowerInvariant();
            return char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
        }

        public sealed class LoginRequest
        {
            public string UsernameOrEmail { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string OtpCode { get; set; } = string.Empty;
            public bool ForceOtpResend { get; set; }
        }

        public sealed class GenerateOtpCommand
        {
            public string Email { get; set; } = string.Empty;
        }

        private sealed record TokenResponse(string Token, DateTime ExpiresAtUtc);
        private sealed record ZoomLoginOutcome(
            bool Success,
            string ErrorCode,
            string Message,
            string ZoomStatus,
            string ZoomStatusCode,
            string UiAction)
        {
            public static ZoomLoginOutcome Ok(string zoomStatus, string zoomStatusCode, string uiAction, string message = "")
            {
                return new ZoomLoginOutcome(
                    true,
                    string.Empty,
                    message ?? string.Empty,
                    zoomStatus,
                    zoomStatusCode ?? string.Empty,
                    uiAction);
            }

            public static ZoomLoginOutcome Fail(
                string errorCode,
                string message,
                string zoomStatus,
                string zoomStatusCode,
                string uiAction)
            {
                return new ZoomLoginOutcome(
                    false,
                    errorCode,
                    message,
                    zoomStatus,
                    zoomStatusCode ?? string.Empty,
                    uiAction);
            }
        }
    }
}
