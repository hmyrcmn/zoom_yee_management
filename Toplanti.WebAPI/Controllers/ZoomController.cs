using Toplanti.Core.Entities.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Toplanti.Business.HttpClients;
using Toplanti.Core.Extensisons;
using Toplanti.Core.Utilities.Helper;
using Toplanti.Entities.DTOs;
using Toplanti.Entities.Zoom;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace Toplanti.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ZoomController : ControllerBase
    {
        private readonly IZoom _zoomApi;
        private readonly ISsoApi _ssoApi;

        public ZoomController(IZoom zoomApi, ISsoApi ssoApi)
        {
            _zoomApi = zoomApi;
            _ssoApi = ssoApi;
        }

        [HttpGet("centerperson")]
        public ActionResult GetCenterPerson()
        {
            var userIdClaim = HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId) || userId <= 0)
            {
                return Ok(new List<object>());
            }

            var result = _ssoApi.Person(userId);
            if (result == null)
            {
                return Ok(new List<object>());
            }

            return Ok(result);
        }

        #region new
        [HttpPost("createzoommeeting")]
        public ActionResult CreateZoomMeeting([FromQuery] ZoomAuthRequest zoomAuthRequest, ZoomCreateRequest zoomCreateRequest)
        {
            var result = _zoomApi.CreateZoomMeetingNew(zoomAuthRequest, zoomCreateRequest).Result;
            return Ok(result);
        }

        [HttpGet("meetingparticipants")]
        public ActionResult GetMeetingParticipants([FromQuery] string meetingUUID)
        {
            var result = _zoomApi.GetMeetingParticipantsNew(meetingUUID).Result;
            return Ok(result);
        }

        [HttpDelete("deleteZoomMeeting")]
        public ActionResult DeleteZoomMeeting([FromQuery] double meetingId)
        {
            var result = _zoomApi.DeleteZoomMeetingNew(meetingId).Result;
            return Ok(result);
        }

        [HttpGet("pastmeetingdetails")]
        public ActionResult GetPastMeetingDetails([FromQuery] string meetingId)
        {
            var result = _zoomApi.GetPastMeetingDetailsNew(meetingId).Result;
            return Ok(result);
        }

        [HttpGet("meetingdetails")]
        public ActionResult GetMeetingDetails([FromQuery] string meetingId)
        {
            var result = _zoomApi.GetMeetingDetailsNew(meetingId).Result;
            return Ok(result);

        }

        [HttpGet("usermeetings")]
        public ActionResult GetUserZoomMeetings()
        {
            var result = _zoomApi.GetUserMeetingListNew().Result;
            return Ok(result);
        }

        #endregion



        [HttpGet("usermeetingsold")]
        public ActionResult GetUserZoomMeetingsOld()
        {
            var result = _zoomApi.GetUserMeetingList();
            return Ok(result);
        }

        [HttpGet("ExistUser")]
        public ActionResult GetExistUser()
        {
            var result = _zoomApi.GetExistUser();
            return Ok(result);
        }

        #region Old Zoom

        [HttpPost("createzoommeetingOld")]
        public ActionResult CreateZoomMeetingOld([FromQuery] ZoomAuthRequest zoomAuthRequest, ZoomCreateRequest zoomCreateRequest)
        {
            var result = _zoomApi.CreateZoomMeeting(zoomAuthRequest, zoomCreateRequest);
            return Ok(result);
        }

        [HttpGet("meetingparticipantsOld")]
        public ActionResult GetMeetingParticipantsOld([FromQuery] string meetingId)
        {
            var result = _zoomApi.GetMeetingParticipants(meetingId);
            return Ok(result);
        }

        [HttpGet("pastmeetingdetailsOld")]
        public ActionResult GetPastMeetingDetailsOld([FromQuery] string meetingId)
        {
            var result = _zoomApi.GetPastMeetingDetails(meetingId);
            return Ok(result);
        }
        [HttpGet("meetingdetailsOld")]
        public ActionResult GetMeetingDetailsOld([FromQuery] string meetingId)
        {
            var result = _zoomApi.GetMeetingDetails(meetingId);
            return Ok(result);
        }

        [HttpDelete("deleteZoomMeetingOld")]
        public ActionResult DeleteZoomMeetingOld([FromQuery] double meetingId)
        {
            var result = _zoomApi.DeleteZoomMeeting(meetingId);
            return Ok(result);
        }

        #endregion

        [Authorize]
        [HttpGet("getzoomusers")]
        public ActionResult GetZoomUserList([FromQuery] BaseCo baseCo)
        {
            if (!IsBilisimAdmin())
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Yetkiniz yok" });
            }

            var result = _zoomApi.GetZoomUserList(baseCo);
            return Ok(result);
        }

        [Authorize]
        [HttpPost("createzoomuser")]
        public ActionResult CreateZoomUser([FromBody] AddUserDto request)
        {
            if (!IsBilisimAdmin())
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Yetkiniz yok" });
            }

            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (request == null)
                {
                    return BadRequest(new { success = false, message = "Geçersiz istek gövdesi." });
                }

                var mappedRequest = new ZoomUserCreatedResponse
                {
                    email = request.email ?? string.Empty,
                    first_name = request.first_name ?? request.firstName ?? string.Empty,
                    last_name = request.last_name ?? request.lastName ?? string.Empty,
                    type = request.type ?? 1,
                    password = string.IsNullOrWhiteSpace(request.password) ? string.Empty : request.password.Trim()
                };

                var result = _zoomApi.CreateZoomUser(mappedRequest);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Kullanıcı eklenemedi: {ex.Message}" });
            }
        }

        private bool IsBilisimAdmin()
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            var department = User.Department();
            return IsBilisimDepartment(department);
        }

        private static bool IsBilisimDepartment(string department)
        {
            if (string.IsNullOrWhiteSpace(department))
            {
                return false;
            }

            var normalized = department
                .Trim()
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormD);

            var chars = normalized
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .Select(c => c == 'ı' ? 'i' : c)
                .ToArray();

            return new string(chars) == "bilisim";
        }
    }
}
