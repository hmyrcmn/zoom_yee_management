using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Toplanti.IntegrationTests.TestHelpers
{
    internal sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = _responder(request);
            if (response.RequestMessage == null)
            {
                response.RequestMessage = request;
            }

            return Task.FromResult(response);
        }

        public static HttpResponseMessage Json(HttpStatusCode statusCode, string jsonBody)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}
