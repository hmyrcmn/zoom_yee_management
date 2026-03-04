using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Toplanti.Business.Concrete;
using Toplanti.Core.Utilities.Security.JWT;
using Toplanti.DataAccess.Concrete.EntityFramework.Models;
using Toplanti.Entities.Enums;
using Toplanti.IntegrationTests.TestHelpers;

namespace Toplanti.IntegrationTests.Security
{
    public class ZoomMeetingSecurityTests
    {
        [Fact]
        public async Task DeleteMeeting_ShouldReturnForbidden_WhenActorIsNotOwner()
        {
            using var context = TestDbContextFactory.CreateContext();
            var ownerUserId = Guid.NewGuid();
            var attackerUserId = Guid.NewGuid();

            context.AuthUsers.AddRange(
                new AuthUser
                {
                    UserId = ownerUserId,
                    Email = "owner@yee.org.tr",
                    EmailNormalized = "OWNER@YEE.ORG.TR",
                    Department = "Bilisim",
                    IsInternal = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new AuthUser
                {
                    UserId = attackerUserId,
                    Email = "attacker@yee.org.tr",
                    EmailNormalized = "ATTACKER@YEE.ORG.TR",
                    Department = "Bilisim",
                    IsInternal = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });

            var meetingId = Guid.NewGuid();
            context.ZoomMeetings.Add(new ZoomMeeting
            {
                MeetingId = meetingId,
                OwnerUserId = ownerUserId,
                ZoomMeetingId = "987654321",
                Topic = "Owner private meeting",
                Agenda = "Private agenda",
                StartTimeUtc = DateTime.UtcNow.AddDays(1),
                DurationMinutes = 30,
                Timezone = "UTC",
                JoinUrl = "https://zoom.us/j/987654321",
                StartUrl = "https://zoom.us/s/987654321",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var actorUserIdFromJwt = ExtractUserIdFromJwt(CreateJwt(attackerUserId, "attacker@yee.org.tr"));

            var clientFactory = new Mock<IHttpClientFactory>();
            clientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(System.Net.HttpStatusCode.NoContent, string.Empty))));

            var tokenHelper = new Mock<ITokenHelper>();
            tokenHelper.Setup(x => x.CreateAccessToken()).ReturnsAsync("mock-token");

            var config = TestConfigurationFactory.Create(("ZoomApi:BaseUrl", "https://api.zoom.us/v2/"));

            var sut = new ZoomMeetingService(
                context,
                clientFactory.Object,
                tokenHelper.Object,
                config,
                NullLogger<ZoomMeetingService>.Instance);

            var result = await sut.DeleteMeetingAsync(actorUserIdFromJwt, meetingId);

            result.Success.Should().BeFalse();
            result.Code.Should().Be("ZOOM_MEETING_NOT_FOUND_OR_FORBIDDEN");

            var meeting = context.ZoomMeetings.Single(x => x.MeetingId == meetingId);
            meeting.IsDeleted.Should().BeFalse();
        }

        private static string CreateJwt(Guid userId, string email)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("UnitTest_ThisKeyMustBeLongEnough_ForJwtSigning_123456"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: "yee-toplanti",
                audience: "yee-toplanti",
                claims: new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Email, email),
                    new Claim("department", "Bilisim")
                },
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static Guid ExtractUserIdFromJwt(string jwtToken)
        {
            var token = new JwtSecurityTokenHandler().ReadJwtToken(jwtToken);
            var raw = token.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value;
            return Guid.Parse(raw);
        }
    }
}
