using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Toplanti.Business.HttpClients;
using Toplanti.Core.Utilities.Helper;
using Toplanti.Entities.Zoom;

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
            var userId = new UserCookie().UserId();
            var result = _ssoApi.Person(userId);
            return Ok(result);
        }

        [HttpPost("createzoommeeting")]
        public ActionResult CreateZoomMeeting([FromQuery] ZoomAuthRequest zoomAuthRequest, ZoomCreateRequest zoomCreateRequest)
        {
            var result = _zoomApi.CreateZoomMeeting(zoomAuthRequest, zoomCreateRequest);
            return Ok(result);
        }

        [HttpGet("usermeetings")]
        public ActionResult GetUserZoomMeetings()
        {
            var result = _zoomApi.GetUserMeetingList();
            return Ok(result);
        }


        [HttpGet("meetingdetails")]
        public ActionResult GetMeetingDetails([FromQuery] string meetingId)
        {
            var result = _zoomApi.GetMeetingDetails(meetingId);
            return Ok(result);
        }

        [HttpGet("meetingparticipants")]
        public ActionResult GetMeetingParticipants([FromQuery] string meetingId)
        {
            var result = _zoomApi.GetMeetingParticipants(meetingId);
            return Ok(result);
        }

        [HttpGet("pastmeetingdetails")]
        public ActionResult GetPastMeetingDetails([FromQuery] string meetingId)
        {
            var result = _zoomApi.GetPastMeetingDetails(meetingId);
            return Ok(result);
        }

        [HttpDelete("deleteZoomMeeting")]
        public ActionResult DeleteZoomMeeting([FromQuery] double meetingId)
        {
            var result = _zoomApi.DeleteZoomMeeting(meetingId);
            return Ok(result);
        }
    }
}
