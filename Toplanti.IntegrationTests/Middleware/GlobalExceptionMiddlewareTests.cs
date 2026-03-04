using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Toplanti.WebAPI.Middleware;

namespace Toplanti.IntegrationTests.Middleware
{
    public class GlobalExceptionMiddlewareTests
    {
        [Fact]
        public async Task InvokeAsync_ShouldReturnStandardJson_WhenUnhandledExceptionOccurs()
        {
            var logger = new Mock<ILogger<GlobalExceptionMiddleware>>();
            RequestDelegate next = _ => throw new InvalidOperationException("boom");
            var middleware = new GlobalExceptionMiddleware(next, logger.Object);

            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            await middleware.InvokeAsync(context);

            context.Response.Body.Position = 0;
            using var reader = new StreamReader(context.Response.Body);
            var body = await reader.ReadToEndAsync();
            var json = JsonDocument.Parse(body);

            Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
            Assert.False(json.RootElement.GetProperty("Success").GetBoolean());
            Assert.Equal("INTERNAL_SERVER_ERROR", json.RootElement.GetProperty("ErrorCode").GetString());
            Assert.Equal("An unexpected error occurred.", json.RootElement.GetProperty("Message").GetString());

            logger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((_, _) => true),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
                Times.Once);
        }
    }
}
