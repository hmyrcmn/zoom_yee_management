using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toplanti.Business.Constants;
using Toplanti.Business.Abstract;
using Toplanti.Entities.DTOs.ZoomProvisioning;

namespace Toplanti.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ZoomProvisioningController : ControllerBase
    {
        private readonly IZoomProvisioningService _zoomProvisioningService;

        public ZoomProvisioningController(IZoomProvisioningService zoomProvisioningService)
        {
            _zoomProvisioningService = zoomProvisioningService;
        }

        [HttpGet("status")]
        public async Task<ActionResult> GetStatus(
            [FromQuery] string email,
            [FromQuery] bool forceResend,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new
                {
                    Success = false,
                    ErrorCode = "INVALID_REQUEST",
                    Message = "Email is required."
                });
            }

            var result = await _zoomProvisioningService.CheckAccountStatusAsync(
                new CheckZoomAccountStatusRequest
                {
                    Email = email,
                    ActorUserId = TryGetActorUserId(),
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                    ForceRefresh = true,
                    ForceActivationInviteResend = forceResend
                },
                cancellationToken);

            if (!result.Success)
            {
                return BadRequest(new
                {
                    Success = false,
                    ErrorCode = result.Code,
                    Message = result.Message
                });
            }

            return Ok(result);
        }

        [HttpPost("request-account")]
        public async Task<ActionResult> RequestAccount(
            [FromBody] ProvisionZoomUserRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    Success = false,
                    ErrorCode = "INVALID_REQUEST",
                    Message = "Request payload is required."
                });
            }

            request.ActorUserId = TryGetActorUserId();
            request.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

            var result = await _zoomProvisioningService.ProvisionUserAsync(request, cancellationToken);
            if (!result.Success)
            {
                var errorCode = result.Code == ZoomProvisioningResultCodes.ProvisioningFailed
                    ? AuthFlowCodes.ZoomAutoProvisionFailed
                    : result.Code;

                return BadRequest(new
                {
                    Success = false,
                    ErrorCode = errorCode,
                    Message = result.Message,
                    RetryAfterSeconds = result.RetryAfterSeconds
                });
            }

            return Ok(result);
        }

        private Guid? TryGetActorUserId()
        {
            var raw = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(raw, out var guid))
            {
                return guid;
            }

            return null;
        }
    }
}
