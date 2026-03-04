using System;
using System.DirectoryServices.Protocols;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Toplanti.Business.Abstract;
using Toplanti.Business.Constants;
using Toplanti.Core.Entities.Concrete;
using Toplanti.DataAccess.Concrete.EntityFramework.Contexts;
using Toplanti.DataAccess.Concrete.EntityFramework.Models;
using Toplanti.Entities.DTOs.Auth;

namespace Toplanti.Business.Concrete
{
    public class AuthenticationService : IAuthenticationService
    {
        private const byte LoginPurpose = 1;
        private const byte EmailDeliveryChannel = 1;
        private readonly ToplantiContext _context;
        private readonly IAuthNotificationService _authNotificationService;
        private readonly ILogger<AuthenticationService> _logger;
        private readonly LdapSettings _ldapSettings;
        private readonly string _internalDomain;
        private readonly int _otpTtlSeconds;
        private readonly int _otpCooldownSeconds;
        private readonly short _otpMaxAttempts;
        private readonly bool _otpAllowConsoleFallback;
        private readonly bool _otpSmtpConfigured;

        public AuthenticationService(
            ToplantiContext context,
            IConfiguration configuration,
            IAuthNotificationService authNotificationService,
            ILogger<AuthenticationService> logger)
        {
            _context = context;
            _authNotificationService = authNotificationService;
            _logger = logger;
            _ldapSettings = configuration.GetSection("LdapSettings").Get<LdapSettings>() ?? new LdapSettings();
            _internalDomain = (configuration["AuthFlow:CorporateDomain"] ?? "yee.org.tr").Trim().ToLowerInvariant();
            _otpTtlSeconds = Math.Max(60, configuration.GetValue<int?>("Otp:CodeTtlSeconds") ?? 300);
            _otpCooldownSeconds = Math.Max(15, configuration.GetValue<int?>("Otp:CooldownSeconds") ?? 60);
            _otpMaxAttempts = (short)Math.Clamp(configuration.GetValue<int?>("Otp:MaxAttempts") ?? 5, 1, 10);
            _otpAllowConsoleFallback = configuration.GetValue<bool>("Otp:AllowConsoleFallback");
            _otpSmtpConfigured =
                !string.IsNullOrWhiteSpace(configuration["Otp:Smtp:Host"])
                && !string.IsNullOrWhiteSpace(configuration["Otp:Smtp:Username"])
                && !string.IsNullOrWhiteSpace(configuration["Otp:Smtp:Password"])
                && !string.IsNullOrWhiteSpace(configuration["Otp:Smtp:FromEmail"]);
        }

        public async Task<AuthenticationResult> AuthenticateLdapAsync(
            AuthenticateLdapRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null
                || string.IsNullOrWhiteSpace(request.UsernameOrEmail)
                || string.IsNullOrWhiteSpace(request.Password))
            {
                return AuthenticationFailure(
                    AuthenticationResultCodes.InvalidRequest,
                    "Username/email ve password zorunludur.");
            }

            var usernameOrEmail = request.UsernameOrEmail.Trim();
            _logger.LogInformation("LDAP authentication started for identity: {Identity}", usernameOrEmail);

            LdapValidationOutcome ldapOutcome;
            try
            {
                ldapOutcome = await ValidateAgainstLdapAsync(usernameOrEmail, request.Password, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected LDAP authentication error for identity: {Identity}", usernameOrEmail);
                return AuthenticationFailure(
                    AuthenticationResultCodes.UnexpectedError,
                    "LDAP kimlik doğrulaması sırasında beklenmeyen bir hata oluştu.");
            }

            if (!ldapOutcome.Success)
            {
                _logger.LogWarning(
                    "LDAP authentication failed for identity: {Identity}. Code: {Code}",
                    usernameOrEmail,
                    ldapOutcome.Code);

                return AuthenticationFailure(
                    ldapOutcome.Code,
                    ldapOutcome.Message);
            }

            var resolvedEmail = NormalizeEmail(ldapOutcome.Email);
            if (string.IsNullOrWhiteSpace(resolvedEmail))
            {
                return AuthenticationFailure(
                    AuthenticationResultCodes.LdapUserInfoNotFound,
                    "LDAP kaydından geçerli email bilgisi alınamadı.");
            }

            if (!IsInternalEmail(resolvedEmail))
            {
                return AuthenticationFailure(
                    AuthenticationResultCodes.LdapInvalidCredentials,
                    "LDAP doğrulaması yalnızca kurumsal kullanıcılar için geçerlidir.");
            }

            var normalizedEmail = NormalizeEmailKey(resolvedEmail);
            var user = await _context.AuthUsers
                .FirstOrDefaultAsync(x => x.EmailNormalized == normalizedEmail, cancellationToken);

            var isNewUser = false;
            if (user == null)
            {
                isNewUser = true;
                user = new AuthUser
                {
                    UserId = Guid.NewGuid(),
                    Email = resolvedEmail,
                    EmailNormalized = normalizedEmail,
                    DisplayName = ResolveDisplayName(ldapOutcome.DisplayName, resolvedEmail),
                    Department = ldapOutcome.Department,
                    IsInternal = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                };

                await _context.AuthUsers.AddAsync(user, cancellationToken);
            }
            else
            {
                user.Email = resolvedEmail;
                user.EmailNormalized = normalizedEmail;
                user.DisplayName = ResolveDisplayName(ldapOutcome.DisplayName, resolvedEmail);
                user.Department = ldapOutcome.Department;
                user.IsInternal = true;
                user.IsActive = true;
                user.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "LDAP authentication succeeded for {Email}. AutoProvisioned: {AutoProvisioned}",
                resolvedEmail,
                isNewUser);

            return new AuthenticationResult
            {
                Success = true,
                Code = isNewUser ? AuthenticationResultCodes.LdapUserProvisioned : AuthenticationResultCodes.LdapAuthenticated,
                Message = isNewUser
                    ? "LDAP doğrulaması başarılı. Kullanıcı veritabanına eklendi."
                    : "LDAP doğrulaması başarılı.",
                UserId = user.UserId,
                Email = user.Email,
                DisplayName = user.DisplayName ?? string.Empty,
                IsInternal = user.IsInternal,
                SessionToken = string.Empty
            };
        }

        public async Task<OtpGenerationResult> GenerateOtpAsync(
            GenerateOtpRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return OtpGenerationFailure(
                    AuthenticationResultCodes.InvalidRequest,
                    "Email zorunludur.");
            }

            var email = NormalizeEmail(request.Email);
            if (!IsValidEmail(email))
            {
                return OtpGenerationFailure(
                    AuthenticationResultCodes.InvalidRequest,
                    "Geçerli bir email adresi girilmelidir.");
            }

            var emailKey = NormalizeEmailKey(email);
            var now = DateTime.UtcNow;

            var latestChallenge = await _context.AuthOtpChallenges
                .AsNoTracking()
                .Where(x => x.EmailNormalized == emailKey && x.Purpose == LoginPurpose)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestChallenge != null)
            {
                var elapsedSeconds = (int)(now - latestChallenge.CreatedAt).TotalSeconds;
                if (elapsedSeconds < _otpCooldownSeconds)
                {
                    var remaining = _otpCooldownSeconds - Math.Max(0, elapsedSeconds);
                    _logger.LogWarning("OTP cooldown triggered for {Email}. Remaining: {RemainingSeconds}s", email, remaining);
                    return OtpGenerationFailure(
                        AuthenticationResultCodes.OtpCooldown,
                        $"Yeni OTP istemek için {remaining} saniye bekleyin.",
                        remaining);
                }
            }

            var otpCode = GenerateSecureSixDigitCode();
            var otpSalt = RandomNumberGenerator.GetBytes(32);
            var otpHash = ComputeOtpHash(otpCode, otpSalt);

            var user = await _context.AuthUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.EmailNormalized == emailKey, cancellationToken);

            var challenge = new AuthOtpChallenge
            {
                OtpChallengeId = Guid.NewGuid(),
                UserId = user?.UserId,
                EmailNormalized = emailKey,
                Purpose = LoginPurpose,
                OtpCodeHash = otpHash,
                OtpCodeSalt = otpSalt,
                AttemptCount = 0,
                MaxAttempts = _otpMaxAttempts,
                ExpiresAt = now.AddSeconds(_otpTtlSeconds),
                DeliveryChannel = EmailDeliveryChannel,
                RequestIpAddress = (request.IpAddress ?? string.Empty).Trim(),
                CorrelationId = Guid.NewGuid(),
                CreatedAt = now
            };

            await _context.AuthOtpChallenges.AddAsync(challenge, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var delivered = await _authNotificationService.SendOtpCode(email, otpCode);
            if (!delivered)
            {
                _context.AuthOtpChallenges.Remove(challenge);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogError("OTP delivery failed for {Email}. Challenge removed.", email);
                return OtpGenerationFailure(
                    AuthenticationResultCodes.OtpDeliveryFailed,
                    "OTP kodu gönderilemedi. Lütfen tekrar deneyin.");
            }

            _logger.LogInformation(
                "OTP generated for {Email}. ChallengeId: {ChallengeId}, ExpiresAt: {ExpiresAt}",
                email,
                challenge.OtpChallengeId,
                challenge.ExpiresAt);

            return new OtpGenerationResult
            {
                Success = true,
                Code = AuthenticationResultCodes.OtpGenerated,
                Message = _otpAllowConsoleFallback && !_otpSmtpConfigured
                    ? "OTP kodu olusturuldu. SMTP ayari olmadigi icin kod API konsoluna yazdirildi."
                    : "OTP kodu başarıyla oluşturuldu.",
                ChallengeId = challenge.OtpChallengeId,
                ExpiresAtUtc = challenge.ExpiresAt,
                CooldownSecondsRemaining = _otpCooldownSeconds
            };
        }

        public async Task<OtpVerificationResult> VerifyOtpAsync(
            VerifyOtpRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null
                || string.IsNullOrWhiteSpace(request.Email)
                || string.IsNullOrWhiteSpace(request.OtpCode))
            {
                return OtpVerificationFailure(
                    AuthenticationResultCodes.InvalidRequest,
                    "Email ve OTP code zorunludur.");
            }

            var email = NormalizeEmail(request.Email);
            if (!IsValidEmail(email))
            {
                return OtpVerificationFailure(
                    AuthenticationResultCodes.InvalidRequest,
                    "Geçerli bir email adresi girilmelidir.");
            }

            var emailKey = NormalizeEmailKey(email);
            var now = DateTime.UtcNow;

            var activeChallenge = await _context.AuthOtpChallenges
                .Where(x => x.EmailNormalized == emailKey
                            && x.Purpose == LoginPurpose
                            && x.ConsumedAt == null)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (activeChallenge == null || activeChallenge.ExpiresAt <= now)
            {
                _logger.LogWarning("OTP verify failed: challenge not found or expired for {Email}", email);
                return OtpVerificationFailure(
                    AuthenticationResultCodes.OtpExpiredOrNotFound,
                    "OTP kodu bulunamadı veya süresi doldu.");
            }

            if (activeChallenge.AttemptCount >= activeChallenge.MaxAttempts)
            {
                if (activeChallenge.ConsumedAt == null)
                {
                    activeChallenge.ConsumedAt = now;
                    await _context.SaveChangesAsync(cancellationToken);
                }

                _logger.LogWarning("OTP challenge locked for {Email}. ChallengeId: {ChallengeId}", email, activeChallenge.OtpChallengeId);
                return OtpVerificationFailure(
                    AuthenticationResultCodes.OtpLocked,
                    "Maksimum deneme sayısı aşıldı. Yeni OTP talep edin.",
                    true,
                    0);
            }

            var inputHash = ComputeOtpHash(request.OtpCode.Trim(), activeChallenge.OtpCodeSalt);
            var isValid = CryptographicOperations.FixedTimeEquals(inputHash, activeChallenge.OtpCodeHash);

            if (!isValid)
            {
                activeChallenge.AttemptCount = (short)Math.Min(short.MaxValue, activeChallenge.AttemptCount + 1);
                var remaining = Math.Max(0, activeChallenge.MaxAttempts - activeChallenge.AttemptCount);

                if (activeChallenge.AttemptCount >= activeChallenge.MaxAttempts)
                {
                    activeChallenge.ConsumedAt = now;
                    await _context.SaveChangesAsync(cancellationToken);
                    _logger.LogWarning("OTP locked after max attempts for {Email}", email);

                    return OtpVerificationFailure(
                        AuthenticationResultCodes.OtpLocked,
                        "Maksimum deneme sayısı aşıldı. Yeni OTP talep edin.",
                        true,
                        0);
                }

                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogWarning("OTP invalid for {Email}. Remaining attempts: {Remaining}", email, remaining);

                return OtpVerificationFailure(
                    AuthenticationResultCodes.OtpInvalid,
                    "OTP kodu geçersiz.",
                    false,
                    remaining);
            }

            activeChallenge.ConsumedAt = now;

            var user = await _context.AuthUsers
                .FirstOrDefaultAsync(x => x.EmailNormalized == emailKey, cancellationToken);

            var isNewUser = false;
            if (user == null)
            {
                isNewUser = true;
                user = new AuthUser
                {
                    UserId = Guid.NewGuid(),
                    Email = email,
                    EmailNormalized = emailKey,
                    DisplayName = ResolveDisplayName(string.Empty, email),
                    Department = string.Empty,
                    IsInternal = IsInternalEmail(email),
                    IsActive = true,
                    CreatedAt = now
                };
                await _context.AuthUsers.AddAsync(user, cancellationToken);
            }
            else
            {
                user.IsActive = true;
                user.UpdatedAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "OTP verification succeeded for {Email}. AutoProvisioned: {AutoProvisioned}",
                email,
                isNewUser);

            return new OtpVerificationResult
            {
                Success = true,
                Code = isNewUser ? AuthenticationResultCodes.OtpUserProvisioned : AuthenticationResultCodes.OtpVerified,
                Message = isNewUser
                    ? "OTP doğrulaması başarılı. Kullanıcı veritabanına eklendi."
                    : "OTP doğrulaması başarılı.",
                UserId = user.UserId,
                Email = user.Email,
                IsLocked = false,
                RemainingAttempts = Math.Max(0, activeChallenge.MaxAttempts - activeChallenge.AttemptCount)
            };
        }

        private async Task<LdapValidationOutcome> ValidateAgainstLdapAsync(
            string usernameOrEmail,
            string password,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var host = string.IsNullOrWhiteSpace(_ldapSettings.Host) ? "172.17.60.20" : _ldapSettings.Host.Trim();
                var port = _ldapSettings.Port > 0 ? _ldapSettings.Port : 389;
                var domain = string.IsNullOrWhiteSpace(_ldapSettings.Domain) ? "yee.org.tr" : _ldapSettings.Domain.Trim();
                var baseDn = string.IsNullOrWhiteSpace(_ldapSettings.BaseDn) ? "DC=yee,DC=org,DC=tr" : _ldapSettings.BaseDn.Trim();
                var rawUsername = ExtractRawUsername(usernameOrEmail);
                var userUpn = usernameOrEmail.Contains("@", StringComparison.Ordinal)
                    ? usernameOrEmail.Trim()
                    : $"{rawUsername}@{domain}";

                try
                {
                    var identifier = new LdapDirectoryIdentifier(host, port, false, false);
                    using var connection = new LdapConnection(identifier)
                    {
                        AuthType = AuthType.Basic,
                        Timeout = TimeSpan.FromSeconds(10)
                    };

                    connection.SessionOptions.ProtocolVersion = 3;
                    connection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;

                    var bindCandidates = new[] { userUpn, rawUsername }
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var isBound = false;
                    foreach (var candidate in bindCandidates)
                    {
                        try
                        {
                            connection.Bind(new NetworkCredential(candidate, password));
                            isBound = true;
                            break;
                        }
                        catch (LdapException ex)
                        {
                            _logger.LogWarning(
                                "LDAP bind failed for candidate {Candidate}. ErrorCode: {ErrorCode}",
                                candidate,
                                ex.ErrorCode);
                        }
                    }

                    if (!isBound)
                    {
                        return LdapValidationOutcome.Failed(
                            AuthenticationResultCodes.LdapInvalidCredentials,
                            "LDAP kullanıcı adı veya şifre hatalı.");
                    }

                    var filter = $"(&(objectClass=user)(sAMAccountName={EscapeLdapFilterValue(rawUsername)}))";
                    var searchRequest = new SearchRequest(
                        baseDn,
                        filter,
                        SearchScope.Subtree,
                        new[] { "mail", "displayName", "department", "givenName", "sn" });

                    var searchResponse = (SearchResponse)connection.SendRequest(searchRequest);
                    var entry = searchResponse.Entries.Cast<SearchResultEntry>().FirstOrDefault();

                    if (entry == null && usernameOrEmail.Contains("@", StringComparison.Ordinal))
                    {
                        var mailFilter = $"(&(objectClass=user)(mail={EscapeLdapFilterValue(usernameOrEmail.Trim())}))";
                        var mailSearchRequest = new SearchRequest(
                            baseDn,
                            mailFilter,
                            SearchScope.Subtree,
                            new[] { "mail", "displayName", "department", "givenName", "sn" });
                        var mailSearchResponse = (SearchResponse)connection.SendRequest(mailSearchRequest);
                        entry = mailSearchResponse.Entries.Cast<SearchResultEntry>().FirstOrDefault();
                    }

                    var resolvedEmail = ReadLdapAttribute(entry, "mail");
                    if (string.IsNullOrWhiteSpace(resolvedEmail))
                    {
                        resolvedEmail = usernameOrEmail.Contains("@", StringComparison.Ordinal)
                            ? usernameOrEmail.Trim()
                            : $"{rawUsername}@{domain}";
                    }

                    var displayName = ReadLdapAttribute(entry, "displayName");
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        var givenName = ReadLdapAttribute(entry, "givenName");
                        var surname = ReadLdapAttribute(entry, "sn");
                        displayName = $"{givenName} {surname}".Trim();
                    }

                    var department = ReadLdapAttribute(entry, "department");

                    return LdapValidationOutcome.Succeeded(
                        resolvedEmail,
                        displayName,
                        department);
                }
                catch (LdapException ex)
                {
                    _logger.LogError(ex, "LDAP connection failure for identity: {Identity}", usernameOrEmail);
                    return LdapValidationOutcome.Failed(
                        AuthenticationResultCodes.LdapConnectionFailed,
                        "LDAP servisine bağlanılamadı.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected LDAP operation failure for identity: {Identity}", usernameOrEmail);
                    return LdapValidationOutcome.Failed(
                        AuthenticationResultCodes.UnexpectedError,
                        "LDAP işlemi sırasında beklenmeyen bir hata oluştu.");
                }
            }, cancellationToken);
        }

        private static string ExtractRawUsername(string usernameOrEmail)
        {
            var normalized = (usernameOrEmail ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            var atIndex = normalized.IndexOf('@');
            return atIndex > 0 ? normalized.Substring(0, atIndex) : normalized;
        }

        private static string EscapeLdapFilterValue(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\5c", StringComparison.Ordinal)
                .Replace("*", "\\2a", StringComparison.Ordinal)
                .Replace("(", "\\28", StringComparison.Ordinal)
                .Replace(")", "\\29", StringComparison.Ordinal)
                .Replace("\0", "\\00", StringComparison.Ordinal);
        }

        private static string ReadLdapAttribute(SearchResultEntry? entry, string attributeName)
        {
            if (entry == null || string.IsNullOrWhiteSpace(attributeName))
            {
                return string.Empty;
            }

            if (entry.Attributes.Contains(attributeName))
            {
                var values = entry.Attributes[attributeName]?.GetValues(typeof(string));
                if (values != null && values.Length > 0)
                {
                    return (values[0]?.ToString() ?? string.Empty).Trim();
                }
            }

            return string.Empty;
        }

        private static string GenerateSecureSixDigitCode()
        {
            var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
            return value.ToString("D6", CultureInfo.InvariantCulture);
        }

        private static byte[] ComputeOtpHash(string code, byte[] salt)
        {
            var normalizedCode = (code ?? string.Empty).Trim();
            var codeBytes = Encoding.UTF8.GetBytes(normalizedCode);

            using var hmac = new HMACSHA256(salt);
            return hmac.ComputeHash(codeBytes);
        }

        private string NormalizeEmail(string email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }

        private string NormalizeEmailKey(string email)
        {
            return NormalizeEmail(email).ToUpperInvariant();
        }

        private bool IsInternalEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(_internalDomain))
            {
                return false;
            }

            return email.EndsWith("@" + _internalDomain, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            try
            {
                _ = new MailAddress(email);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveDisplayName(string? displayName, string email)
        {
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName.Trim();
            }

            var localPart = (email ?? string.Empty).Split('@').FirstOrDefault();
            return string.IsNullOrWhiteSpace(localPart) ? "Unknown User" : localPart.Trim();
        }

        private static AuthenticationResult AuthenticationFailure(string code, string message)
        {
            return new AuthenticationResult
            {
                Success = false,
                Code = code,
                Message = message
            };
        }

        private static OtpGenerationResult OtpGenerationFailure(string code, string message, int cooldownSecondsRemaining = 0)
        {
            return new OtpGenerationResult
            {
                Success = false,
                Code = code,
                Message = message,
                CooldownSecondsRemaining = Math.Max(0, cooldownSecondsRemaining)
            };
        }

        private static OtpVerificationResult OtpVerificationFailure(string code, string message, bool isLocked = false, int remainingAttempts = 0)
        {
            return new OtpVerificationResult
            {
                Success = false,
                Code = code,
                Message = message,
                IsLocked = isLocked,
                RemainingAttempts = Math.Max(0, remainingAttempts)
            };
        }

        private sealed class LdapValidationOutcome
        {
            public bool Success { get; init; }
            public string Code { get; init; } = string.Empty;
            public string Message { get; init; } = string.Empty;
            public string Email { get; init; } = string.Empty;
            public string DisplayName { get; init; } = string.Empty;
            public string Department { get; init; } = string.Empty;

            public static LdapValidationOutcome Succeeded(string email, string displayName, string department)
            {
                return new LdapValidationOutcome
                {
                    Success = true,
                    Code = AuthenticationResultCodes.LdapAuthenticated,
                    Message = "LDAP doğrulaması başarılı.",
                    Email = email ?? string.Empty,
                    DisplayName = displayName ?? string.Empty,
                    Department = department ?? string.Empty
                };
            }

            public static LdapValidationOutcome Failed(string code, string message)
            {
                return new LdapValidationOutcome
                {
                    Success = false,
                    Code = code,
                    Message = message
                };
            }
        }
    }
}
