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
    public class ZoomMeetingDuplicateTests
    {
        [Fact]
        public async Task CreateMeeting_ShouldRejectDuplicate_WhenMeetingDetailsAreIdentical()
        {
            using var context = TestDbContextFactory.CreateContext();

            var actorUserId = Guid.NewGuid();
            var provisioningId = Guid.NewGuid();
            var targetDate = DateTime.UtcNow.AddDays(2);
            var existingStartTime = new DateTime(
                targetDate.Year,
                targetDate.Month,
                targetDate.Day,
                targetDate.Hour,
                targetDate.Minute,
                25,
                DateTimeKind.Utc);

            context.AuthUsers.Add(new AuthUser
            {
                UserId = actorUserId,
                Email = "owner@yee.org.tr",
                EmailNormalized = "OWNER@YEE.ORG.TR",
                Department = "Bilisim",
                IsInternal = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            context.ZoomUserProvisionings.Add(new ZoomUserProvisioning
            {
                UserProvisioningId = provisioningId,
                UserId = actorUserId,
                Email = "owner@yee.org.tr",
                EmailNormalized = "OWNER@YEE.ORG.TR",
                ZoomUserId = "owner-zoom-id",
                ZoomStatusId = (byte)ZoomProvisioningStatus.Active,
                CreatedAt = DateTime.UtcNow
            });

            context.ZoomMeetings.Add(new ZoomMeeting
            {
                MeetingId = Guid.NewGuid(),
                OwnerUserId = actorUserId,
                UserProvisioningId = provisioningId,
                ZoomMeetingId = "999888777",
                Topic = "Haftalik Planlama",
                Agenda = "Sprint plani",
                StartTimeUtc = existingStartTime,
                DurationMinutes = 60,
                Timezone = "Europe/Istanbul",
                JoinUrl = "https://zoom.us/j/999888777",
                StartUrl = "https://zoom.us/s/999888777",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();

            var clientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
            var tokenHelper = new Mock<ITokenHelper>(MockBehavior.Strict);
            var config = TestConfigurationFactory.Create(("ZoomApi:BaseUrl", "https://api.zoom.us/v2/"));

            var sut = new ZoomMeetingService(
                context,
                clientFactory.Object,
                tokenHelper.Object,
                config,
                NullLogger<ZoomMeetingService>.Instance);

            var request = new CreateZoomMeetingRequest
            {
                Topic = " Haftalik Planlama ",
                Agenda = "Sprint plani",
                StartTimeUtc = new DateTime(
                    targetDate.Year,
                    targetDate.Month,
                    targetDate.Day,
                    targetDate.Hour,
                    targetDate.Minute,
                    0,
                    DateTimeKind.Utc),
                DurationMinutes = 60,
                Timezone = "Europe/Istanbul"
            };

            var result = await sut.CreateMeetingAsync(actorUserId, request);

            result.Success.Should().BeFalse();
            result.Code.Should().Be(ZoomMeetingResultCodes.MeetingDuplicate);
            result.Message.Should().NotBeNullOrWhiteSpace();

            context.ZoomMeetings.Count(x => x.OwnerUserId == actorUserId && !x.IsDeleted).Should().Be(1);
            context.AuditZoomActionLogs.Count(x => x.ResultCode == ZoomMeetingResultCodes.MeetingDuplicate).Should().Be(1);
            clientFactory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
            tokenHelper.Verify(x => x.CreateAccessToken(), Times.Never);
        }
    }
}
