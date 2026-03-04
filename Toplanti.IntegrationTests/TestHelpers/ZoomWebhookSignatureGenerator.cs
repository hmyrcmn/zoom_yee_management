using System.Security.Cryptography;
using System.Text;

namespace Toplanti.IntegrationTests.TestHelpers
{
    internal static class ZoomWebhookSignatureGenerator
    {
        public static string Generate(string secretToken, string timestamp, string payloadJson)
        {
            var canonical = $"v0:{timestamp}:{payloadJson}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretToken ?? string.Empty));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            return "v0=" + Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
