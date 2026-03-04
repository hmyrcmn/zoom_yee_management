using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Toplanti.Business.Abstract;
using Toplanti.Business.Constants;
using Toplanti.Entities.DTOs.ZoomProvisioning;

namespace Toplanti.WebAPI.Controllers
{
    [Route("api/webhooks")]
    [ApiController]
    [AllowAnonymous]
    public class WebhookController : ControllerBase
    {
        private const string ZoomSignatureHeader = "x-zm-signature";
        private const string ZoomTimestampHeader = "x-zm-request-timestamp";

        private readonly IZoomProvisioningService _zoomProvisioningService;
        private readonly IConfiguration _configuration;

        public WebhookController(
            IZoomProvisioningService zoomProvisioningService,
            IConfiguration configuration)
        {
            _zoomProvisioningService = zoomProvisioningService;
            _configuration = configuration;
        }

        [HttpPost("zoom")]
        public async Task<ActionResult> ReceiveZoomWebhook(CancellationToken cancellationToken)
        {
            var payload = await ReadRawBodyAsync(Request, cancellationToken);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return BadRequest(new
                {
                    Success = false,
                    ErrorCode = "INVALID_REQUEST",
                    Message = "Request body is required."
                });
            }

            var signature = Request.Headers[ZoomSignatureHeader].ToString();
            var timestamp = Request.Headers[ZoomTimestampHeader].ToString();
            var eventType = ReadJsonProperty(payload, "event");
            var eventId = ReadJsonProperty(payload, "event_id");

            var callbackResult = await _zoomProvisioningService.HandleCallbackAsync(
                new ZoomCallbackRequest
                {
                    EventId = eventId,
                    EventType = eventType,
                    Signature = signature,
                    Timestamp = timestamp,
                    PayloadJson = payload,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty
                },
                cancellationToken);

            if (!callbackResult.Success)
            {
                if (string.Equals(
                    callbackResult.Code,
                    ZoomProvisioningResultCodes.CallbackInvalidSignature,
                    StringComparison.Ordinal))
                {
                    return Unauthorized(new
                    {
                        Success = false,
                        ErrorCode = callbackResult.Code,
                        Message = callbackResult.Message
                    });
                }

                return BadRequest(new
                {
                    Success = false,
                    ErrorCode = callbackResult.Code,
                    Message = callbackResult.Message
                });
            }

            if (string.Equals(eventType, "endpoint.url_validation", StringComparison.OrdinalIgnoreCase))
            {
                var plainToken = ReadJsonProperty(payload, "payload", "plainToken");
                if (string.IsNullOrWhiteSpace(plainToken))
                {
                    return BadRequest(new
                    {
                        Success = false,
                        ErrorCode = "INVALID_CHALLENGE",
                        Message = "Zoom plainToken is missing."
                    });
                }

                var secretToken = _configuration["ZoomWebhook:SecretToken"] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(secretToken))
                {
                    return BadRequest(new
                    {
                        Success = false,
                        ErrorCode = "WEBHOOK_SECRET_MISSING",
                        Message = "Zoom webhook secret token is not configured."
                    });
                }

                return Ok(new
                {
                    plainToken,
                    encryptedToken = ComputeZoomChallengeToken(plainToken, secretToken)
                });
            }

            return Ok(new
            {
                Success = true,
                ErrorCode = string.Empty,
                Message = "Webhook processed.",
                EventType = callbackResult.EventType
            });
        }

        private static async Task<string> ReadRawBodyAsync(Microsoft.AspNetCore.Http.HttpRequest request, CancellationToken cancellationToken)
        {
            request.EnableBuffering();
            request.Body.Position = 0;

            using var reader = new StreamReader(request.Body, Encoding.UTF8, false, 4096, leaveOpen: true);
            var payload = await reader.ReadToEndAsync(cancellationToken);
            request.Body.Position = 0;
            return payload;
        }

        private static string ReadJsonProperty(string json, params string[] path)
        {
            if (string.IsNullOrWhiteSpace(json) || path == null || path.Length == 0)
            {
                return string.Empty;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var current = doc.RootElement;
                foreach (var segment in path)
                {
                    if (!current.TryGetProperty(segment, out var next))
                    {
                        return string.Empty;
                    }

                    current = next;
                }

                return current.ValueKind == JsonValueKind.String
                    ? current.GetString() ?? string.Empty
                    : current.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ComputeZoomChallengeToken(string plainToken, string secretToken)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretToken));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(plainToken));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
