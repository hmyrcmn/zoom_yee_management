using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Toplanti.Business.Constants;
using Toplanti.DataAccess.Concrete.EntityFramework.Models;
using Toplanti.Entities.DTOs.Auth;
using Toplanti.IntegrationTests.TestHelpers;
using Toplanti.WebAPI.Controllers;

namespace Toplanti.IntegrationTests.Controllers
{
    public class AuthControllerIntegrationTests
    {
        [Fact]
        public async Task Login_ShouldReturnJwtWithUserClaims_WhenLdapAuthenticationSucceeds()
        {
            using var context = TestDbContextFactory.CreateContext();
            var userId = Guid.NewGuid();
            var email = "ldap.user@yee.org.tr";
            context.AuthUsers.Add(new AuthUser
            {
                UserId = userId,
                Email = email,
                EmailNormalized = email.ToUpperInvariant(),
                Department = "Bilisim",
                DisplayName = "LDAP User",
                IsInternal = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var authService = new MockAuthenticationService
            {
                AuthenticateLdapHandler = _ => new AuthenticationResult
                {
                    Success = true,
                    Code = AuthenticationResultCodes.LdapAuthenticated,
                    Message = "ok",
                    UserId = userId,
                    Email = email,
                    IsInternal = true
                }
            };

            var config = TestConfigurationFactory.Create(
                ("TokenOptions:Issuer", "yee-toplanti"),
                ("TokenOptions:Audience", "yee-toplanti"),
                ("TokenOptions:SecurityKey", "UnitTest_ThisKeyMustBeLongEnough_ForJwtSigning_123456"),
                ("TokenOptions:AccessTokenExpiration", "60"));
            var zoomProvisioningService = new MockZoomProvisioningService
            {
                CheckStatusHandler = _ => new Toplanti.Entities.DTOs.ZoomProvisioning.ZoomAccountStatusResult
                {
                    Success = true,
                    Code = "ZOOM_STATUS_FETCHED",
                    Message = "ok",
                    StatusName = "Active"
                }
            };

            var sut = new AuthController(authService, zoomProvisioningService, context, config)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            var actionResult = await sut.Login(new AuthController.LoginRequest
            {
                UsernameOrEmail = email,
                Password = "any"
            }, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(actionResult);
            var json = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));

            Assert.True(json.RootElement.GetProperty("Success").GetBoolean());
            var token = json.RootElement.GetProperty("Data").GetProperty("Token").GetString();
            Assert.False(string.IsNullOrWhiteSpace(token));

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            Assert.Equal(userId.ToString(), jwt.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value);
            Assert.Equal(email, jwt.Claims.First(x => x.Type == "email").Value);
            Assert.Equal("Bilisim", jwt.Claims.First(x => x.Type == "department").Value);
        }

        [Fact]
        public async Task Login_ShouldReturnUnauthorized_WhenLdapAuthenticationFails()
        {
            using var context = TestDbContextFactory.CreateContext();
            var authService = new MockAuthenticationService
            {
                AuthenticateLdapHandler = _ => new AuthenticationResult
                {
                    Success = false,
                    Code = AuthenticationResultCodes.LdapInvalidCredentials,
                    Message = "invalid credentials"
                }
            };

            var config = TestConfigurationFactory.Create(
                ("TokenOptions:Issuer", "yee-toplanti"),
                ("TokenOptions:Audience", "yee-toplanti"),
                ("TokenOptions:SecurityKey", "UnitTest_ThisKeyMustBeLongEnough_ForJwtSigning_123456"),
                ("TokenOptions:AccessTokenExpiration", "60"));
            var zoomProvisioningService = new MockZoomProvisioningService();
            var sut = new AuthController(authService, zoomProvisioningService, context, config)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            var actionResult = await sut.Login(new AuthController.LoginRequest
            {
                UsernameOrEmail = "bad.user@yee.org.tr",
                Password = "bad"
            }, CancellationToken.None);

            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(actionResult);
            var json = JsonDocument.Parse(JsonSerializer.Serialize(unauthorized.Value));
            Assert.False(json.RootElement.GetProperty("Success").GetBoolean());
            Assert.Equal(AuthenticationResultCodes.LdapInvalidCredentials, json.RootElement.GetProperty("ErrorCode").GetString());
        }

        [Fact]
        public async Task VerifyOtp_ShouldReturnJwt_WhenOtpVerificationSucceeds()
        {
            using var context = TestDbContextFactory.CreateContext();
            var userId = Guid.NewGuid();
            var email = "otp.user@example.com";
            context.AuthUsers.Add(new AuthUser
            {
                UserId = userId,
                Email = email,
                EmailNormalized = email.ToUpperInvariant(),
                Department = "External",
                DisplayName = "OTP User",
                IsInternal = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var authService = new MockAuthenticationService
            {
                VerifyOtpHandler = _ => new OtpVerificationResult
                {
                    Success = true,
                    Code = AuthenticationResultCodes.OtpVerified,
                    Message = "ok",
                    UserId = userId,
                    Email = email,
                    IsLocked = false,
                    RemainingAttempts = 4
                }
            };

            var config = TestConfigurationFactory.Create(
                ("TokenOptions:Issuer", "yee-toplanti"),
                ("TokenOptions:Audience", "yee-toplanti"),
                ("TokenOptions:SecurityKey", "UnitTest_ThisKeyMustBeLongEnough_ForJwtSigning_123456"),
                ("TokenOptions:AccessTokenExpiration", "60"));
            var zoomProvisioningService = new MockZoomProvisioningService
            {
                CheckStatusHandler = _ => new Toplanti.Entities.DTOs.ZoomProvisioning.ZoomAccountStatusResult
                {
                    Success = true,
                    Code = "ZOOM_STATUS_FETCHED",
                    Message = "ok",
                    StatusName = "Active"
                }
            };

            var sut = new AuthController(authService, zoomProvisioningService, context, config)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            var actionResult = await sut.VerifyOtp(new VerifyOtpRequest
            {
                Email = email,
                OtpCode = "123456"
            }, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(actionResult);
            var json = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
            Assert.True(json.RootElement.GetProperty("Success").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("Data").GetProperty("Token").GetString()));
        }
    }
}
