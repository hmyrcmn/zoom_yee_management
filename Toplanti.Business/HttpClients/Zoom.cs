


using Core.Utilities.Results;
using Core.Utilities.Security.JWT;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using Toplanti.Business.Constants;
using Toplanti.Core.Utilities.Helper;
using Toplanti.Entities.Zoom;

namespace Toplanti.Business.HttpClients
{
    public class Zoom : IZoom
    {
        public string APIName = "ZoomApi";
        public string BASE_API_URL = "https://api.zoom.us/v2/";
        //public string AUTH_API_URL = "https://zoom.us/oauth/token/";
        //public string Client_Id = "rE9mNzJSn643cUmx2u6NA";
        //public string Client_Secret = "SvzAecPOfRkNUa5uGUgKMo1aGCrvpc0T";

        private readonly ISsoApi _ssoApi;
        private IHttpClientFactory _httpClientFactory;
        private ITokenHelper _tokenHelper;

        public Zoom(ISsoApi ssoApi, IHttpClientFactory httpClientFactory, ITokenHelper tokenHelper)
        {
            _ssoApi = ssoApi;
            _httpClientFactory = httpClientFactory;
            _tokenHelper = tokenHelper;
        }

        public IDataResult<ZoomCreatedResponse> CreateZoomMeeting(ZoomAuthRequest zoomAuthRequest, ZoomCreateRequest zoomCreateRequest)
        {
            GetZoomUserList();

            var userId = new UserCookie().UserId();

            var userZoomId = "me";

            var centerPersonZoomId = _ssoApi.Person(userId)?.ZoomId;
            if (centerPersonZoomId != null)
            {
                userZoomId = centerPersonZoomId;
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

            return new SuccessDataResult<ZoomCreatedResponse>(zoomCreatedResponse, Messages.ZoomCreated);
        }

        public IDataResult<AccessToken> CreateZoomJwtToken()
        {
            var jwtToken = _tokenHelper.CreateZoomToken();

            return new SuccessDataResult<AccessToken>(jwtToken, Messages.AccessTokenCreated);
        }

        public IResult GetZoomUserList()
        {
            var userId = new UserCookie().UserId();

            var jwtToken = _tokenHelper.CreateZoomToken();

            var client = _httpClientFactory.CreateClient(APIName);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken.Token);

            var person = _ssoApi.GetEmailByUserId(userId);

            HttpResponseMessage response = client.GetAsync(BASE_API_URL + "users?page_number=0&page_size=20000").Result;

            if (response.IsSuccessStatusCode)
            {
                var zoomUsers = JsonConvert.DeserializeObject<ZoomUserList>(response.Content.ReadAsStringAsync().Result);

                if (zoomUsers.users != null)
                    foreach (var zmUser in zoomUsers.users)
                    {
                        var ssoUser = _ssoApi.GetUserOnlyEmail(zmUser.email);

                        if (ssoUser != null)
                        {
                            HttpResponseMessage userMeetings = client.GetAsync(BASE_API_URL + "users/" + zmUser.id + "meetings").Result;
                            var userMeetingList = JsonConvert.DeserializeObject<ZoomUserList>(userMeetings.Content.ReadAsStringAsync().Result);

                            var isDeleteableUser = true;

                            if (userMeetingList.meetings != null)
                                foreach (var userMeeting in userMeetingList.meetings)
                                {
                                    if (userMeeting.start_time.AddHours(4) > DateTime.Now)
                                    {
                                        isDeleteableUser = false;
                                    }
                                }
                            if (isDeleteableUser && zmUser.type != 1 && zmUser.email != person.Email)
                            {
                                zmUser.type = 1;
                                HttpContent content = JsonContent.Create(zmUser);
                                client.PatchAsync(BASE_API_URL + "users/" + zmUser.id, content);
                            }
                        }

                        if (zmUser.email == person.Email)
                        {
                            zmUser.type = 2;
                            HttpContent content = JsonContent.Create(zmUser);
                            client.PatchAsync(BASE_API_URL + "users/" + zmUser.id, content);
                        }
                    }
            }
            else
            {
                return new ErrorResult(Messages.ZoomCreateError);
            }
            return new SuccessResult(Messages.ZoomCreated);
        }
    }
}
