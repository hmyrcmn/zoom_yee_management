using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Toplanti.Business.Abstract;
using Toplanti.Business.Constants;
using Toplanti.Entities.DTOs.ZoomMeetings;

namespace Toplanti.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MeetingsController : ControllerBase
    {
        private readonly IZoomMeetingService _zoomMeetingService;

        public MeetingsController(IZoomMeetingService zoomMeetingService)
        {
            _zoomMeetingService = zoomMeetingService;
        }

        [HttpPost]
        public async Task<ActionResult> CreateMeeting(
            [FromBody] CreateZoomMeetingRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetActorUserId(out var actorUserId))
            {
                return Unauthorized(new
                {
                    Success = false,
                    ErrorCode = ZoomMeetingResultCodes.InvalidRequest,
                    Message = "Authenticated actor is required."
                });
            }

            var result = await _zoomMeetingService.CreateMeetingAsync(actorUserId, request, cancellationToken);
            return ToMeetingActionResult(result);
        }

        [HttpGet("history")]
        public async Task<ActionResult> GetHistory(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetActorUserId(out var actorUserId))
            {
                return Unauthorized(new
                {
                    Success = false,
                    ErrorCode = ZoomMeetingResultCodes.InvalidRequest,
                    Message = "Authenticated actor is required."
                });
            }

            var result = await _zoomMeetingService.GetHistoryAsync(
                actorUserId,
                pageNumber,
                pageSize,
                cancellationToken);

            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(new
            {
                Success = false,
                ErrorCode = result.Code,
                Message = result.Message
            });
        }

        [HttpGet("~/api/Zoom/usermeetings")]
        public async Task<ActionResult> GetLegacyUserMeetings(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 200,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetActorUserId(out var actorUserId))
            {
                return Unauthorized(new
                {
                    Success = false,
                    ErrorCode = ZoomMeetingResultCodes.InvalidRequest,
                    Message = "Authenticated actor is required."
                });
            }

            var result = await _zoomMeetingService.GetHistoryAsync(
                actorUserId,
                pageNumber,
                pageSize,
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

            return Ok(new
            {
                Success = true,
                Message = result.Message,
                Data = result.Meetings,
                Meetings = result.Meetings,
                PageNumber = result.PageNumber,
                PageSize = result.PageSize,
                TotalCount = result.TotalCount
            });
        }

        [HttpGet("{meetingId:guid}")]
        public async Task<ActionResult> GetMeetingById(
            Guid meetingId,
            CancellationToken cancellationToken)
        {
            if (!TryGetActorUserId(out var actorUserId))
            {
                return Unauthorized(new
                {
                    Success = false,
                    ErrorCode = ZoomMeetingResultCodes.InvalidRequest,
                    Message = "Authenticated actor is required."
                });
            }

            var result = await _zoomMeetingService.GetMeetingByIdAsync(
                actorUserId,
                meetingId,
                cancellationToken);

            return ToMeetingActionResult(result);
        }

        [HttpDelete("{meetingId:guid}")]
        public async Task<ActionResult> DeleteMeeting(
            Guid meetingId,
            CancellationToken cancellationToken)
        {
            if (!TryGetActorUserId(out var actorUserId))
            {
                return Unauthorized(new
                {
                    Success = false,
                    ErrorCode = ZoomMeetingResultCodes.InvalidRequest,
                    Message = "Authenticated actor is required."
                });
            }

            var result = await _zoomMeetingService.DeleteMeetingAsync(
                actorUserId,
                meetingId,
                cancellationToken);

            return ToMeetingActionResult(result);
        }

        private ActionResult ToMeetingActionResult(ZoomMeetingOperationResult result)
        {
            if (result.Success)
            {
                return Ok(result);
            }

            if (string.Equals(
                    result.Code,
                    ZoomMeetingResultCodes.MeetingNotFoundOrForbidden,
                    StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    Success = false,
                    ErrorCode = result.Code,
                    Message = result.Message
                });
            }

            if (string.Equals(
                    result.Code,
                    ZoomMeetingResultCodes.InvalidRequest,
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    Success = false,
                    ErrorCode = result.Code,
                    Message = result.Message
                });
            }

            if (string.Equals(
                    result.Code,
                    ZoomMeetingResultCodes.MeetingDuplicate,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new
                {
                    Success = false,
                    ErrorCode = result.Code,
                    Message = result.Message
                });
            }

            return BadRequest(new
            {
                Success = false,
                ErrorCode = result.Code,
                Message = result.Message
            });
        }

        private bool TryGetActorUserId(out Guid actorUserId)
        {
            var raw = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out actorUserId) && actorUserId != Guid.Empty;
        }
    }
}
