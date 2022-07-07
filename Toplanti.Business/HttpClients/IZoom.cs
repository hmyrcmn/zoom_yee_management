using Core.Entities.Concrete;
using Core.Utilities.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toplanti.Entities.DTOs;
using Toplanti.Entities.Zoom;

namespace Toplanti.Business.HttpClients
{
    public interface IZoom
    {
        public IDataResult<ZoomCreatedResponse> CreateZoomMeeting(ZoomAuthRequest zoomAuthRequest, ZoomCreateRequest zoomCreateRequest);
        public IResult DeleteZoomMeeting(double meetingId);
        public IDataResult<List<UserMeetings>> GetUserMeetingList();
        public IDataResult<PastMeetingDetails> GetPastMeetingDetails(string meetingId);
        public IDataResult<ZoomCreatedResponse> GetMeetingDetails(string meetingId);
        public IDataResult<List<Participants>> GetMeetingParticipants(string meetingId);
        public IDataResult<ZoomUserListWithCo> GetZoomUserList(BaseCo baseCo);
        public IResult CreateZoomUser(ZoomUserCreatedResponse request);
    }
}
