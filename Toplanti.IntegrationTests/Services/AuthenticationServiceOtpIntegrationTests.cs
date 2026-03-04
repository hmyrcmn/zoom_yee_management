using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Toplanti.Business.Concrete;
using Toplanti.Business.Constants;
using Toplanti.DataAccess.Concrete.EntityFramework.Models;
using Toplanti.Entities.DTOs.Auth;
using Toplanti.IntegrationTests.TestHelpers;

namespace Toplanti.IntegrationTests.Services
{
    public class AuthenticationServiceOtpIntegrationTests
    {
        [Fact]
        public async Task GenerateOtpAsync_ShouldStoreHashedOtp_AndSetExpectedExpiry()
        {
            using var context = TestDbContextFactory.CreateContext();
            var notifier = new OtpCaptureNotificationService();
            var config = TestConfigurationFactory.Create(
                ("AuthFlow:CorporateDomain", "yee.org.tr"),
                ("Otp:CodeTtlSeconds", "300"),
                ("Otp:CooldownSeconds", "60"),
                ("Otp:MaxAttempts", "5"),
                ("LdapSettings:Host", "localhost"),
                ("LdapSettings:Port", "389"),
                ("LdapSettings:BaseDn", "DC=example,DC=local"),
                ("LdapSettings:Domain", "example.local"));

            var sut = new AuthenticationService(
                context,
                config,
                notifier,
                NullLogger<AuthenticationService>.Instance);

            var before = DateTime.UtcNow;
            var result = await sut.GenerateOtpAsync(new GenerateOtpRequest
            {
                Email = "external.user@example.com",
                IpAddress = "127.0.0.1"
            });
            var after = DateTime.UtcNow;

            Assert.True(result.Success);
            Assert.NotNull(result.ChallengeId);
            Assert.False(string.IsNullOrWhiteSpace(notifier.LastCode));
            Assert.Equal(6, notifier.LastCode.Length);
            Assert.True(notifier.LastCode.All(char.IsDigit));

            var challenge = context.AuthOtpChallenges.Single(x => x.OtpChallengeId == result.ChallengeId!.Value);
            Assert.NotEmpty(challenge.OtpCodeSalt);
            Assert.NotEmpty(challenge.OtpCodeHash);

            // OTP should be stored as hash, never as plain text.
            Assert.False(Encoding.UTF8.GetBytes(notifier.LastCode).SequenceEqual(challenge.OtpCodeHash));

            using var hmac = new HMACSHA256(challenge.OtpCodeSalt);
            var expectedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(notifier.LastCode));
            Assert.True(CryptographicOperations.FixedTimeEquals(expectedHash, challenge.OtpCodeHash));

            var expectedMin = before.AddMinutes(5).AddSeconds(-5);
            var expectedMax = after.AddMinutes(5).AddSeconds(5);
            Assert.InRange(challenge.ExpiresAt, expectedMin, expectedMax);
        }

        [Fact]
        public async Task GenerateOtpAsync_ShouldEnforceCooldown()
        {
            using var context = TestDbContextFactory.CreateContext();
            var notifier = new OtpCaptureNotificationService();
            var config = TestConfigurationFactory.Create(
                ("AuthFlow:CorporateDomain", "yee.org.tr"),
                ("Otp:CodeTtlSeconds", "300"),
                ("Otp:CooldownSeconds", "60"),
                ("Otp:MaxAttempts", "5"),
                ("LdapSettings:Host", "localhost"),
                ("LdapSettings:Port", "389"),
                ("LdapSettings:BaseDn", "DC=example,DC=local"),
                ("LdapSettings:Domain", "example.local"));

            var sut = new AuthenticationService(
                context,
                config,
                notifier,
                NullLogger<AuthenticationService>.Instance);

            var first = await sut.GenerateOtpAsync(new GenerateOtpRequest
            {
                Email = "cooldown.user@example.com",
                IpAddress = "127.0.0.1"
            });
            var second = await sut.GenerateOtpAsync(new GenerateOtpRequest
            {
                Email = "cooldown.user@example.com",
                IpAddress = "127.0.0.1"
            });

            Assert.True(first.Success);
            Assert.False(second.Success);
            Assert.Equal("OTP_COOLDOWN", second.Code);
            Assert.InRange(second.CooldownSecondsRemaining, 1, 60);
        }

        [Fact]
        public async Task Otp_ShouldFail_AfterMaxAttempts()
        {
            using var context = TestDbContextFactory.CreateContext();
            var notifier = new OtpCaptureNotificationService();
            var config = TestConfigurationFactory.Create(
                ("AuthFlow:CorporateDomain", "yee.org.tr"),
                ("Otp:CodeTtlSeconds", "300"),
                ("Otp:CooldownSeconds", "60"),
                ("Otp:MaxAttempts", "5"),
                ("LdapSettings:Host", "localhost"),
                ("LdapSettings:Port", "389"),
                ("LdapSettings:BaseDn", "DC=example,DC=local"),
                ("LdapSettings:Domain", "example.local"));

            var sut = new AuthenticationService(
                context,
                config,
                notifier,
                NullLogger<AuthenticationService>.Instance);

            var email = "attempts.user@example.com";
            var generated = await sut.GenerateOtpAsync(new GenerateOtpRequest
            {
                Email = email,
                IpAddress = "127.0.0.1"
            });

            generated.Success.Should().BeTrue();
            notifier.LastCode.Should().NotBeNullOrWhiteSpace();

            var wrongCode = notifier.LastCode == "000000" ? "999999" : "000000";

            for (var i = 1; i <= 4; i++)
            {
                var invalid = await sut.VerifyOtpAsync(new VerifyOtpRequest
                {
                    Email = email,
                    OtpCode = wrongCode,
                    IpAddress = "127.0.0.1"
                });

                invalid.Success.Should().BeFalse();
                invalid.Code.Should().Be(AuthenticationResultCodes.OtpInvalid);
                invalid.IsLocked.Should().BeFalse();
                invalid.RemainingAttempts.Should().Be(5 - i);
            }

            var locked = await sut.VerifyOtpAsync(new VerifyOtpRequest
            {
                Email = email,
                OtpCode = wrongCode,
                IpAddress = "127.0.0.1"
            });

            locked.Success.Should().BeFalse();
            locked.Code.Should().Be(AuthenticationResultCodes.OtpLocked);
            locked.IsLocked.Should().BeTrue();
            locked.RemainingAttempts.Should().Be(0);
        }

        [Fact]
        public async Task VerifyOtpAsync_ShouldFail_WhenChallengeIsExpired()
        {
            using var context = TestDbContextFactory.CreateContext();
            var email = "expired.user@example.com";
            var emailKey = email.ToUpperInvariant();

            context.AuthOtpChallenges.Add(new AuthOtpChallenge
            {
                OtpChallengeId = Guid.NewGuid(),
                EmailNormalized = emailKey,
                Purpose = 1,
                OtpCodeSalt = RandomNumberGenerator.GetBytes(32),
                OtpCodeHash = RandomNumberGenerator.GetBytes(32),
                AttemptCount = 0,
                MaxAttempts = 5,
                ExpiresAt = DateTime.UtcNow.AddSeconds(-1),
                DeliveryChannel = 1,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            });
            await context.SaveChangesAsync();

            var config = TestConfigurationFactory.Create(
                ("AuthFlow:CorporateDomain", "yee.org.tr"),
                ("Otp:CodeTtlSeconds", "300"),
                ("Otp:CooldownSeconds", "60"),
                ("Otp:MaxAttempts", "5"),
                ("LdapSettings:Host", "localhost"),
                ("LdapSettings:Port", "389"),
                ("LdapSettings:BaseDn", "DC=example,DC=local"),
                ("LdapSettings:Domain", "example.local"));

            var sut = new AuthenticationService(
                context,
                config,
                new OtpCaptureNotificationService(),
                NullLogger<AuthenticationService>.Instance);

            var result = await sut.VerifyOtpAsync(new VerifyOtpRequest
            {
                Email = email,
                OtpCode = "000000",
                IpAddress = "127.0.0.1"
            });

            Assert.False(result.Success);
            Assert.Equal(AuthenticationResultCodes.OtpExpiredOrNotFound, result.Code);
        }
    }
}
