using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Toplanti.Business.Concrete;
using Toplanti.Core.Utilities.Security.JWT;
using Toplanti.IntegrationTests.TestHelpers;
using Toplanti.WebAPI.Controllers;

namespace Toplanti.IntegrationTests.Security
{
    public class WebhookSecurityTests
    {
        [Fact]
        public async Task ReceiveZoomWebhook_ShouldReturnUnauthorized_WhenSignatureIsInvalid()
        {
            using var context = TestDbContextFactory.CreateContext();

            var clientFactory = new Mock<IHttpClientFactory>();
            clientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(System.Net.HttpStatusCode.OK, "{}"))));

            var tokenHelper = new Mock<ITokenHelper>();
            tokenHelper.Setup(x => x.CreateAccessToken()).ReturnsAsync("mock-token");

            var config = TestConfigurationFactory.Create(
                ("ZoomApi:BaseUrl", "https://api.zoom.us/v2/"),
                ("ZoomWebhook:SecretToken", "unit-test-secret"),
                ("ZoomWebhook:TimestampToleranceSeconds", "300"),
                ("ZoomWebhook:SkipSignatureValidation", "false"));

            var provisioningService = new ZoomProvisioningService(
                context,
                clientFactory.Object,
                tokenHelper.Object,
                config,
                NullLogger<ZoomProvisioningService>.Instance);

            var controller = new WebhookController(provisioningService, config);

            var payload = "{\"event\":\"user.activated\",\"event_id\":\"evt-1\",\"payload\":{\"object\":{\"email\":\"webhook.user@yee.org.tr\",\"id\":\"zoom-user-1\"}}}";
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = HttpMethods.Post;
            httpContext.Request.ContentType = "application/json";
            httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
            httpContext.Request.Headers["x-zm-request-timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            httpContext.Request.Headers["x-zm-signature"] = "v0=invalidsignature";
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var result = await controller.ReceiveZoomWebhook(CancellationToken.None);

            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
            var json = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(unauthorized.Value));
            json.RootElement.GetProperty("Success").GetBoolean().Should().BeFalse();
            json.RootElement.GetProperty("ErrorCode").GetString().Should().Be("ZOOM_CALLBACK_INVALID_SIGNATURE");
        }

        [Fact]
        public async Task WebhookSimulator_ShouldAcceptUserActivatedPayload_WhenSignatureIsValid()
        {
            using var context = TestDbContextFactory.CreateContext();

            var clientFactory = new Mock<IHttpClientFactory>();
            clientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(System.Net.HttpStatusCode.OK, "{}"))));

            var tokenHelper = new Mock<ITokenHelper>();
            tokenHelper.Setup(x => x.CreateAccessToken()).ReturnsAsync("mock-token");

            var config = TestConfigurationFactory.Create(
                ("ZoomApi:BaseUrl", "https://api.zoom.us/v2/"),
                ("ZoomWebhook:SecretToken", "unit-test-secret"),
                ("ZoomWebhook:TimestampToleranceSeconds", "300"),
                ("ZoomWebhook:SkipSignatureValidation", "false"));

            var provisioningService = new ZoomProvisioningService(
                context,
                clientFactory.Object,
                tokenHelper.Object,
                config,
                NullLogger<ZoomProvisioningService>.Instance);

            var controller = new WebhookController(provisioningService, config);

            var payload = "{\"event\":\"user.activated\",\"event_id\":\"evt-valid-1\",\"payload\":{\"object\":{\"email\":\"webhook.valid@yee.org.tr\",\"id\":\"zoom-user-valid-1\"}}}";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var signature = ZoomWebhookSignatureGenerator.Generate("unit-test-secret", timestamp, payload);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = HttpMethods.Post;
            httpContext.Request.ContentType = "application/json";
            httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
            httpContext.Request.Headers["x-zm-request-timestamp"] = timestamp;
            httpContext.Request.Headers["x-zm-signature"] = signature;
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var result = await controller.ReceiveZoomWebhook(CancellationToken.None);

            result.Should().BeOfType<OkObjectResult>();
        }
    }
}
