using Core.Utilities.Results;
using Core.Utilities.Security.JWT;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using Toplanti.Business.Constants;
using Toplanti.Business.Helpers;
using Toplanti.Core.Utilities.Helper;
using Toplanti.Entities.Zoom;

namespace Toplanti.Business.HttpClients
{
    public class Zoom : IZoom
    {
        public string APIName = "ZoomApi";
        public string BASE_API_URL = "https://api.zoom.us/v2/";
        private readonly ISsoApi _ssoApi;
        private IHttpClientFactory _httpClientFactory;
        private ITokenHelper _tokenHelper;
        private IEmailHelper _emailHelper;

        public Zoom(ISsoApi ssoApi, IHttpClientFactory httpClientFactory, ITokenHelper tokenHelper, IEmailHelper emailHelper)
        {
            _ssoApi = ssoApi;
            _httpClientFactory = httpClientFactory;
            _tokenHelper = tokenHelper;
            _emailHelper = emailHelper;
        }

        public IDataResult<ZoomCreatedResponse> CreateZoomMeeting(ZoomAuthRequest zoomAuthRequest, ZoomCreateRequest zoomCreateRequest)
        {
            var userZoomId = "me";

            var userId = new UserCookie().UserId();

            var personEmail = _ssoApi.GetEmailByUserId(userId).Email;

            var returnedUserZoomId = GetZoomUserList(personEmail);

            if (returnedUserZoomId == "")
            {
                personEmail = "sahibiemail";
            }
            else
            {
                userZoomId = returnedUserZoomId;
            }

            ZoomCreatedResponse zoomCreatedResponse = new ZoomCreatedResponse();

            var jwtToken = _tokenHelper.CreateZoomToken();

            var client = _httpClientFactory.CreateClient(APIName);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken.Token);

            HttpContent content = JsonContent.Create(zoomCreateRequest);

            HttpResponseMessage response = client.PostAsync(BASE_API_URL + "users/" + userZoomId + "/meetings", content).Result;

            if (response.IsSuccessStatusCode)
            {
                zoomCreatedResponse = JsonConvert.DeserializeObject<ZoomCreatedResponse>(response.Content.ReadAsStringAsync().Result);
            }
            else
            {
                return new ErrorDataResult<ZoomCreatedResponse>(zoomCreatedResponse, Messages.ZoomCreateError);
            }

            HttpResponseMessage userResponse = client.GetAsync(BASE_API_URL + "users/" + userZoomId).Result;

            var zoomUser = JsonConvert.DeserializeObject<ZoomUsers>(userResponse.Content.ReadAsStringAsync().Result);

            _emailHelper.OpenedZoom(personEmail, userZoomId, zoomUser.type);

            return new SuccessDataResult<ZoomCreatedResponse>(zoomCreatedResponse, Messages.ZoomCreated);

        }

        public IDataResult<AccessToken> CreateZoomJwtToken()
        {
            var jwtToken = _tokenHelper.CreateZoomToken();

            return new SuccessDataResult<AccessToken>(jwtToken, Messages.AccessTokenCreated);
        }

        public string GetZoomUserList(string personEmail)
        {
            var zoomId = "";

            ZoomUserList zoomUsers = new ZoomUserList();

            var jwtToken = _tokenHelper.CreateZoomToken();

            var client = _httpClientFactory.CreateClient(APIName);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken.Token);

            HttpResponseMessage response = client.GetAsync(BASE_API_URL + "users?page_number=0&page_size=200000").Result;

            if (response.IsSuccessStatusCode)
            {
                zoomUsers = JsonConvert.DeserializeObject<ZoomUserList>(response.Content.ReadAsStringAsync().Result);

                if (zoomUsers.users != null)
                    foreach (var zmUser in zoomUsers.users)
                    {
                        var ssoUser = _ssoApi.GetUserOnlyEmail(zmUser.email);
                        if (ssoUser != null)
                        {
                            HttpResponseMessage userMeetings = client.GetAsync(BASE_API_URL + "users/" + zmUser.id + "/meetings?page_size=99999&type=upcoming").Result;
                            var userMeetingList = JsonConvert.DeserializeObject<ZoomUserList>(userMeetings.Content.ReadAsStringAsync().Result);

                            var isDeleteableUser = true;

                            if (userMeetingList.meetings != null)
                                isDeleteableUser = userMeetingList.meetings.Count < 1;

                            if (isDeleteableUser && zmUser.type != 1 && zmUser.email != personEmail)
                            {
                                zmUser.type = 1;
                                HttpContent content = JsonContent.Create(zmUser);
                                HttpResponseMessage patchUser = client.PatchAsync(BASE_API_URL + "users/" + zmUser.id, content).Result;
                            }

                            if (zmUser.email == personEmail)
                            {
                                zoomId = zmUser.id;
                                zmUser.type = 2;
                                HttpContent content = JsonContent.Create(zmUser);
                                HttpResponseMessage patchUser = client.PatchAsync(BASE_API_URL + "users/" + zmUser.id, content).Result;
                            }
                        }
                    }
            }
            return zoomId;
        }

        public IResult DeleteZoomMeeting(double meetingId)
        {
            var jwtToken = _tokenHelper.CreateZoomToken();
            var client = _httpClientFactory.CreateClient(APIName);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken.Token);

            HttpResponseMessage response = client.DeleteAsync(BASE_API_URL + "meetings/" + meetingId).Result;

            if (response.IsSuccessStatusCode)
            {
                return new SuccessResult(Messages.ZoomDeleted);
            }

            return new ErrorResult(Messages.ZoomDeleteError);
        }
    }
}
