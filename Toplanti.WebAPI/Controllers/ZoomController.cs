using Core.Entities.Concrete;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Toplanti.Business.HttpClients;
using Toplanti.Core.Utilities.Helper;
using Toplanti.Entities.DTOs;
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

        [HttpGet("getzoomusers")]
        public ActionResult GetZoomUserList([FromQuery] BaseCo baseCo)
        {
            var result = _zoomApi.GetZoomUserList(baseCo);
            return Ok(result);
        }

        [HttpPost("createzoomuser")]
        public ActionResult CreateZoomUser(ZoomUserCreatedResponse zoomCreateUserRequest)
        {
            var result = _zoomApi.CreateZoomUser(zoomCreateUserRequest);
            return Ok(result);
        }
    }
}
