using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Toplanti.Business.Concrete;
using Toplanti.Business.Constants;
using Toplanti.Core.Utilities.Security.JWT;
using Toplanti.DataAccess.Concrete.EntityFramework.Models;
using Toplanti.Entities.DTOs.ZoomMeetings;
using Toplanti.Entities.Enums;
using Toplanti.IntegrationTests.TestHelpers;

namespace Toplanti.IntegrationTests.Security
{
    public class ZoomMeetingExternalFallbackTests
    {
        [Fact]
        public async Task CreateMeeting_ShouldFallbackToSharedHost_ForExternalPendingUser_WhenPrimaryHostFails()
        {
            using var context = TestDbContextFactory.CreateContext();

            var actorUserId = Guid.NewGuid();
            var provisioningId = Guid.NewGuid();

            context.AuthUsers.Add(new AuthUser
            {
                UserId = actorUserId,
                Email = "external.user@gmail.com",
                EmailNormalized = "EXTERNAL.USER@GMAIL.COM",
                Department = string.Empty,
                IsInternal = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            context.ZoomUserProvisionings.Add(new ZoomUserProvisioning
            {
                UserProvisioningId = provisioningId,
                UserId = actorUserId,
                Email = "external.user@gmail.com",
                EmailNormalized = "EXTERNAL.USER@GMAIL.COM",
                ZoomUserId = "pending-user-id",
                ZoomStatusId = (byte)ZoomProvisioningStatus.ActivationPending,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();

            var requestPaths = new List<string>();
            var clientFactory = new Mock<IHttpClientFactory>();
            clientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient(new StubHttpMessageHandler(request =>
                {
                    var path = request.RequestUri?.AbsolutePath ?? string.Empty;
                    requestPaths.Add(path);

                    if (path.Contains("/users/pending-user-id/meetings", StringComparison.OrdinalIgnoreCase))
                    {
                        return StubHttpMessageHandler.Json(
                            HttpStatusCode.BadRequest,
                            "{\"message\":\"User does not exist: pending-user-id\"}");
                    }

                    if (path.Contains("/users/me/meetings", StringComparison.OrdinalIgnoreCase))
                    {
                        return StubHttpMessageHandler.Json(
                            HttpStatusCode.Created,
                            "{\"id\":\"123456789\",\"topic\":\"Harici Kullanici Toplantisi\",\"agenda\":\"OTP ile giris\",\"start_time\":\"2026-03-10T09:00:00Z\",\"duration\":45,\"timezone\":\"UTC\",\"join_url\":\"https://zoom.us/j/123456789\",\"start_url\":\"https://zoom.us/s/123456789\"}");
                    }

                    return StubHttpMessageHandler.Json(HttpStatusCode.NotFound, "{\"message\":\"Unexpected path\"}");
                })));

            var tokenHelper = new Mock<ITokenHelper>();
            tokenHelper.Setup(x => x.CreateAccessToken()).ReturnsAsync("mock-access-token");

            var config = TestConfigurationFactory.Create(("ZoomApi:BaseUrl", "https://api.zoom.us/v2/"));

            var sut = new ZoomMeetingService(
                context,
                clientFactory.Object,
                tokenHelper.Object,
                config,
                NullLogger<ZoomMeetingService>.Instance);

            var result = await sut.CreateMeetingAsync(
                actorUserId,
                new CreateZoomMeetingRequest
                {
                    Topic = "Harici Kullanici Toplantisi",
                    Agenda = "OTP ile giris",
                    StartTimeUtc = DateTime.UtcNow.AddDays(1),
                    DurationMinutes = 45,
                    Timezone = "UTC"
                });

            result.Success.Should().BeTrue();
            result.Code.Should().Be(ZoomMeetingResultCodes.MeetingCreated);
            requestPaths.Should().Contain(path => path.Contains("/users/pending-user-id/meetings", StringComparison.OrdinalIgnoreCase));
            requestPaths.Should().Contain(path => path.Contains("/users/me/meetings", StringComparison.OrdinalIgnoreCase));

            var createdMeeting = context.ZoomMeetings.Single(x => x.OwnerUserId == actorUserId && !x.IsDeleted);
            createdMeeting.ZoomMeetingId.Should().Be("123456789");
            createdMeeting.UserProvisioningId.Should().Be(provisioningId);
        }
    }
}
