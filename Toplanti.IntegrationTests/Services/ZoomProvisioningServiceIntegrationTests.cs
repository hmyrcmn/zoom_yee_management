using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Toplanti.Business.Concrete;
using Toplanti.Core.Utilities.Security.JWT;
using Toplanti.DataAccess.Concrete.EntityFramework.Models;
using Toplanti.Entities.DTOs.ZoomProvisioning;
using Toplanti.Entities.Enums;
using Toplanti.IntegrationTests.TestHelpers;

namespace Toplanti.IntegrationTests.Services
{
    public class ZoomProvisioningServiceIntegrationTests
    {
        [Fact]
        public async Task Provisioning_ShouldFollowStateMachine()
        {
            using var context = TestDbContextFactory.CreateContext();
            var email = "state.machine@yee.org.tr";
            context.AuthUsers.Add(new AuthUser
            {
                UserId = Guid.NewGuid(),
                Email = email,
                EmailNormalized = email.ToUpperInvariant(),
                Department = "Bilisim",
                IsInternal = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = CreateService(
                context,
                _ => StubHttpMessageHandler.Json(HttpStatusCode.Created, "{\"id\":\"zoom-state-machine-user\"}"));

            var result = await service.ProvisionUserAsync(new ProvisionZoomUserRequest
            {
                Email = email,
                FirstName = "State",
                LastName = "Machine",
                UserType = 1,
                IpAddress = "127.0.0.1"
            });

            result.Success.Should().BeTrue();

            var provisioning = context.ZoomUserProvisionings.Single(x => x.EmailNormalized == email.ToUpperInvariant());
            var history = context.ZoomUserProvisioningHistories
                .Where(x => x.UserProvisioningId == provisioning.UserProvisioningId)
                .OrderBy(x => x.CreatedAt)
                .ToList();

            history.Should().Contain(x => x.FromStatusId == (byte)ZoomProvisioningStatus.None
                                          && x.ToStatusId == (byte)ZoomProvisioningStatus.ProvisioningPending);
            history.Should().Contain(x => x.FromStatusId == (byte)ZoomProvisioningStatus.ProvisioningPending
                                          && x.ToStatusId == (byte)ZoomProvisioningStatus.ActivationPending);
            history.Should().NotContain(x => x.FromStatusId == (byte)ZoomProvisioningStatus.None
                                             && x.ToStatusId == (byte)ZoomProvisioningStatus.Active);
        }

        [Fact]
        public async Task CheckAccountStatusAsync_ShouldSyncActiveStatus_WhenZoomWorkspaceContainsUser()
        {
            using var context = TestDbContextFactory.CreateContext();
            var email = "sync.user@yee.org.tr";
            context.AuthUsers.Add(new AuthUser
            {
                UserId = Guid.NewGuid(),
                Email = email,
                EmailNormalized = email.ToUpperInvariant(),
                Department = "Bilisim",
                IsInternal = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = CreateService(
                context,
                _ => StubHttpMessageHandler.Json(HttpStatusCode.OK, "{\"id\":\"zoom-123\",\"email\":\"sync.user@yee.org.tr\",\"status\":\"active\"}"));

            var result = await service.CheckAccountStatusAsync(new CheckZoomAccountStatusRequest
            {
                Email = email,
                IpAddress = "127.0.0.1"
            });

            Assert.True(result.Success);
            Assert.Equal("Active", result.StatusName);

            var provisioning = context.ZoomUserProvisionings.Single(x => x.EmailNormalized == email.ToUpperInvariant());
            Assert.Equal((byte)ZoomProvisioningStatus.Active, provisioning.ZoomStatusId);
            Assert.Equal("zoom-123", provisioning.ZoomUserId);

            var historyCount = context.ZoomUserProvisioningHistories.Count(x => x.UserProvisioningId == provisioning.UserProvisioningId);
            Assert.True(historyCount >= 1);
        }

        [Fact]
        public async Task ProvisionUserAsync_ShouldHandle201Created_AndWriteTransitionHistory()
        {
            using var context = TestDbContextFactory.CreateContext();
            var email = "created.user@yee.org.tr";
            context.AuthUsers.Add(new AuthUser
            {
                UserId = Guid.NewGuid(),
                Email = email,
                EmailNormalized = email.ToUpperInvariant(),
                Department = "Bilisim",
                IsInternal = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = CreateService(
                context,
                _ => StubHttpMessageHandler.Json(HttpStatusCode.Created, "{\"id\":\"zoom-created-user\"}"));

            var result = await service.ProvisionUserAsync(new ProvisionZoomUserRequest
            {
                Email = email,
                FirstName = "Created",
                LastName = "User",
                UserType = 1,
                IpAddress = "127.0.0.1"
            });

            Assert.True(result.Success);
            Assert.Equal("ZOOM_PROVISIONING_STARTED", result.Code);

            var provisioning = context.ZoomUserProvisionings.Single(x => x.EmailNormalized == email.ToUpperInvariant());
            Assert.Equal((byte)ZoomProvisioningStatus.ActivationPending, provisioning.ZoomStatusId);

            var history = context.ZoomUserProvisioningHistories
                .Where(x => x.UserProvisioningId == provisioning.UserProvisioningId)
                .OrderBy(x => x.CreatedAt)
                .ToList();

            Assert.True(history.Count >= 2);
            Assert.Equal((byte)ZoomProvisioningStatus.ProvisioningPending, history[0].ToStatusId);
            Assert.Equal((byte)ZoomProvisioningStatus.ActivationPending, history[^1].ToStatusId);
        }

        [Fact]
        public async Task ZoomApi_Conflict_ShouldSetStatusToActive()
        {
            using var context = TestDbContextFactory.CreateContext();
            var email = "conflict.user@yee.org.tr";
            context.AuthUsers.Add(new AuthUser
            {
                UserId = Guid.NewGuid(),
                Email = email,
                EmailNormalized = email.ToUpperInvariant(),
                Department = "Bilisim",
                IsInternal = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = CreateService(
                context,
                _ => StubHttpMessageHandler.Json(HttpStatusCode.Conflict, "{\"id\":\"zoom-existing-user\",\"message\":\"already exists\"}"));

            var result = await service.ProvisionUserAsync(new ProvisionZoomUserRequest
            {
                Email = email,
                FirstName = "Conflict",
                LastName = "User",
                UserType = 1,
                IpAddress = "127.0.0.1"
            });

            Assert.True(result.Success);
            Assert.Equal("ZOOM_PROVISIONING_CONFLICT_ACTIVE", result.Code);

            var provisioning = context.ZoomUserProvisionings.Single(x => x.EmailNormalized == email.ToUpperInvariant());
            Assert.Equal((byte)ZoomProvisioningStatus.Active, provisioning.ZoomStatusId);

            var history = context.ZoomUserProvisioningHistories
                .Where(x => x.UserProvisioningId == provisioning.UserProvisioningId)
                .OrderBy(x => x.CreatedAt)
                .ToList();

            Assert.True(history.Count >= 2);
            Assert.Equal((byte)ZoomProvisioningStatus.ProvisioningPending, history[0].ToStatusId);
            Assert.Equal((byte)ZoomProvisioningStatus.Active, history[^1].ToStatusId);
        }

        [Fact]
        public async Task ProvisionUserAsync_ShouldHandle429RateLimit_AsFailed_AndWriteHistory()
        {
            using var context = TestDbContextFactory.CreateContext();
            var email = "ratelimit.user@yee.org.tr";
            context.AuthUsers.Add(new AuthUser
            {
                UserId = Guid.NewGuid(),
                Email = email,
                EmailNormalized = email.ToUpperInvariant(),
                Department = "Bilisim",
                IsInternal = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = CreateService(
                context,
                _ =>
                {
                    var response = StubHttpMessageHandler.Json(HttpStatusCode.TooManyRequests, "{\"message\":\"Rate limit\"}");
                    response.Headers.TryAddWithoutValidation("Retry-After", "30");
                    return response;
                });

            var result = await service.ProvisionUserAsync(new ProvisionZoomUserRequest
            {
                Email = email,
                FirstName = "Rate",
                LastName = "Limit",
                UserType = 1,
                IpAddress = "127.0.0.1"
            });

            Assert.False(result.Success);
            Assert.Equal("ZOOM_PROVISIONING_RATE_LIMITED", result.Code);
            Assert.Equal(30, result.RetryAfterSeconds);

            var provisioning = context.ZoomUserProvisionings.Single(x => x.EmailNormalized == email.ToUpperInvariant());
            Assert.Equal((byte)ZoomProvisioningStatus.Failed, provisioning.ZoomStatusId);

            var history = context.ZoomUserProvisioningHistories
                .Where(x => x.UserProvisioningId == provisioning.UserProvisioningId)
                .OrderBy(x => x.CreatedAt)
                .ToList();

            Assert.True(history.Count >= 2);
            Assert.Equal((byte)ZoomProvisioningStatus.ProvisioningPending, history[0].ToStatusId);
            Assert.Equal((byte)ZoomProvisioningStatus.Failed, history[^1].ToStatusId);
        }

        private static ZoomProvisioningService CreateService(
            Toplanti.DataAccess.Concrete.EntityFramework.Contexts.ToplantiContext context,
            Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            var handler = new StubHttpMessageHandler(responder);
            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.zoom.us/v2/")
            };

            var clientFactory = new Mock<IHttpClientFactory>();
            clientFactory
                .Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(client);

            var tokenHelper = new Mock<ITokenHelper>();
            tokenHelper.Setup(x => x.CreateAccessToken()).ReturnsAsync("mock-token");

            var config = TestConfigurationFactory.Create(
                ("ZoomApi:BaseUrl", "https://api.zoom.us/v2/"),
                ("ZoomWebhook:SecretToken", "unit-test-secret"),
                ("ZoomWebhook:TimestampToleranceSeconds", "300"),
                ("ZoomWebhook:SkipSignatureValidation", "false"));

            return new ZoomProvisioningService(
                context,
                clientFactory.Object,
                tokenHelper.Object,
                config,
                NullLogger<ZoomProvisioningService>.Instance);
        }
    }
}
