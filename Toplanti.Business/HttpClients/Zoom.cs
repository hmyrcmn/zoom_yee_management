using Core.Entities.Concrete;
using Core.Utilities.Results;
using Core.Utilities.Security.JWT;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using Toplanti.Business.Constants;
using Toplanti.Business.Helpers;
using Toplanti.Core.Utilities.Helper;
using Toplanti.Entities.DTOs;
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

            var returnedUserZoomId = GetUserZoomIdByEmail(personEmail);

            if (returnedUserZoomId == "")
            {
                personEmail = personEmail + " ile sahibiemail";
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

        public string GetUserZoomIdByEmail(string personEmail)
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

                var loggedZoomUser = zoomUsers.users.FirstOrDefault(s => personEmail.Equals(s.email, StringComparison.CurrentCultureIgnoreCase));

                if (zoomUsers.users != null)
                {
                    foreach (var zmUser in zoomUsers.users.Where(s => s.type == 2))
                    {
                        var ssoUser = _ssoApi.GetUserOnlyEmail(zmUser.email);
                        if (ssoUser != null)
                        {
                            HttpResponseMessage userMeetings = client.GetAsync(BASE_API_URL + "users/" + zmUser.id + "/meetings?page_size=99999&type=upcoming").Result;
                            var userMeetingList = JsonConvert.DeserializeObject<ZoomUserList>(userMeetings.Content.ReadAsStringAsync().Result);

                            var isDeleteableUser = true;

                            if (userMeetingList.meetings != null)
                                isDeleteableUser = userMeetingList.meetings.Count < 1;

                            if (isDeleteableUser && zmUser.type != 1 && zmUser.email != personEmail && !zmUser.email.Equals("sevim.aktas@yee.org.tr", StringComparison.CurrentCultureIgnoreCase)
                                && !zmUser.email.Equals("emin.kasikci@yee.org.tr", StringComparison.CurrentCultureIgnoreCase)
                                && !zmUser.email.Equals("emrah.yuzuak@yee.org.tr", StringComparison.CurrentCultureIgnoreCase)
                                && !zmUser.email.Equals("tunahan.cimen@yee.org.tr", StringComparison.CurrentCultureIgnoreCase)
                               )
                            {
                                zmUser.type = 1;
                                HttpContent content = JsonContent.Create(zmUser);
                                HttpResponseMessage patchUser = client.PatchAsync(BASE_API_URL + "users/" + zmUser.id, content).Result;
                            }
                        }
                    }
                    if (loggedZoomUser != null)
                    {
                        zoomId = loggedZoomUser.id;
                        loggedZoomUser.type = 2;
                        HttpContent patchContent = JsonContent.Create(loggedZoomUser);
                        HttpResponseMessage patchLoggedUser = client.PatchAsync(BASE_API_URL + "users/" + loggedZoomUser.id, patchContent).Result;
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

        public IDataResult<List<UserMeetings>> GetUserMeetingList()
        {
            List<UserMeetings> userMeetings = new List<UserMeetings>();

            var userEmail = new UserCookie().Email();

            var zoomId = "";

            ZoomUserList zoomUsers = new ZoomUserList();

            var jwtToken = _tokenHelper.CreateZoomToken();

            var client = _httpClientFactory.CreateClient(APIName);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken.Token);

            HttpResponseMessage zoomUserListResponse = client.GetAsync(BASE_API_URL + "users?page_number=0&page_size=200000").Result;

            if (zoomUserListResponse.IsSuccessStatusCode)
            {
                zoomUsers = JsonConvert.DeserializeObject<ZoomUserList>(zoomUserListResponse.Content.ReadAsStringAsync().Result);

                if (zoomUsers.users != null)
                {
                    zoomId = zoomUsers.users.FirstOrDefault(s => s.email == userEmail)?.id;
                    if (zoomId != "" && zoomId != null)
                    {
                        HttpResponseMessage userMeetingsResponse = client.GetAsync(BASE_API_URL + "users/" + zoomId + "/meetings?page_size=99999999").Result;
                        if (userMeetingsResponse.IsSuccessStatusCode)
                        {
                            userMeetings = JsonConvert.DeserializeObject<ZoomUserList>(userMeetingsResponse.Content.ReadAsStringAsync().Result).meetings.ToList().OrderByDescending(s => s.start_time).ToList();

                        }
                        else
                        {
                            return new ErrorDataResult<List<UserMeetings>>(userMeetings, Messages.UserZoomMeetingsListed);

                        }
                    }
                }
            }
            else
            {
                return new ErrorDataResult<List<UserMeetings>>(userMeetings, Messages.UserZoomMeetingsListed);
            }

            return new SuccessDataResult<List<UserMeetings>>(userMeetings, Messages.UserZoomMeetingsListed);
        }

        public IDataResult<PastMeetingDetails> GetPastMeetingDetails(string meetingId)
        {
            PastMeetingDetails pastMeetingDetails = null;

            var jwtToken = _tokenHelper.CreateZoomToken();

            var client = _httpClientFactory.CreateClient(APIName);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken.Token);

            HttpResponseMessage pastMeetingDetailsResponse = client.GetAsync(BASE_API_URL + "past_meetings/" + meetingId).Result;

            if (pastMeetingDetailsResponse.IsSuccessStatusCode)
            {
                pastMeetingDetails = JsonConvert.DeserializeObject<PastMeetingDetails>(pastMeetingDetailsResponse.Content.ReadAsStringAsync().Result);
            }
            else
            {
                return new ErrorDataResult<PastMeetingDetails>(pastMeetingDetails, Messages.PastMeetingDetailsError);
            }

            return new SuccessDataResult<PastMeetingDetails>(pastMeetingDetails, Messages.PastMeetingDetailsListed);
        }

        public IDataResult<ZoomCreatedResponse> GetMeetingDetails(string meetingId)
        {
            ZoomCreatedResponse meetingDetails = null;

            var jwtToken = _tokenHelper.CreateZoomToken();

            var client = _httpClientFactory.CreateClient(APIName);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken.Token);

            HttpResponseMessage pastMeetingDetailsResponse = client.GetAsync(BASE_API_URL + "meetings/" + meetingId).Result;

            if (pastMeetingDetailsResponse.IsSuccessStatusCode)
            {
                meetingDetails = JsonConvert.DeserializeObject<ZoomCreatedResponse>(pastMeetingDetailsResponse.Content.ReadAsStringAsync().Result);
            }
            else
            {
                return new ErrorDataResult<ZoomCreatedResponse>(meetingDetails, Messages.PastMeetingDetailsError);
            }

            return new SuccessDataResult<ZoomCreatedResponse>(meetingDetails, Messages.PastMeetingDetailsListed);
        }

        public IDataResult<List<Participants>> GetMeetingParticipants(string meetingId)
        {
            ZoomUserList zoomUsers = new ZoomUserList();

            var jwtToken = _tokenHelper.CreateZoomToken();

            var client = _httpClientFactory.CreateClient(APIName);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken.Token);

            HttpResponseMessage pastMeetingDetailsResponse = client.GetAsync(BASE_API_URL + "past_meetings/" + meetingId + "/participants?page_number=0&page_size=200000").Result;

            if (pastMeetingDetailsResponse.IsSuccessStatusCode)
            {
                zoomUsers = JsonConvert.DeserializeObject<ZoomUserList>(pastMeetingDetailsResponse.Content.ReadAsStringAsync().Result);
            }
            else
            {
                return new ErrorDataResult<List<Participants>>(new List<Participants>(), Messages.PastMeetingDetailsError);
            }

            return new SuccessDataResult<List<Participants>>(zoomUsers.participants.GroupBy(x => x.name).Select(y => y.First()).ToList(), Messages.PastMeetingDetailsListed);
        }

        public IDataResult<ZoomUserListWithCo> GetZoomUserList(BaseCo baseCo)
        {
            ZoomUserListWithCo zoomUserListWithCo = new ZoomUserListWithCo();

            var jwtToken = _tokenHelper.CreateZoomToken();

            var client = _httpClientFactory.CreateClient(APIName);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken.Token);

            HttpResponseMessage response = client.GetAsync(BASE_API_URL + "users?page_number=" + baseCo.PageIndex + 1 + "&page_size=" + baseCo.PageSize).Result;

            if (response.IsSuccessStatusCode)
            {
                zoomUserListWithCo = JsonConvert.DeserializeObject<ZoomUserListWithCo>(response.Content.ReadAsStringAsync().Result);
                return new SuccessDataResult<ZoomUserListWithCo>(zoomUserListWithCo, Messages.ProcessSuccess);
            }
            else
            {
                return new ErrorDataResult<ZoomUserListWithCo>(zoomUserListWithCo, Messages.ProcessFailed);
            }
        }

        public IResult CreateZoomUser(ZoomUserCreatedResponse request)
        {
            StudentRegisterDto studentRegisterDto = new StudentRegisterDto()
            {
                Email = request.email,
                FirstName = request.first_name,
                LastName = request.last_name,
                citiesId = 107139,
            };

            var result = _ssoApi.RegisterSsoMeeting(studentRegisterDto);

            if (result)
                return new SuccessResult(Messages.ProcessSuccess);
            else
                return new ErrorResult(Messages.ProcessSuccess);
            //request.type = 1;
            //request.password = request.email.Split("@")[0];
            //ZoomCreateUserRequest zoomCreateUserRequest = new ZoomCreateUserRequest()
            //{
            //    action = "create",
            //    user_info = request
            //};

            //ZoomUserCreatedResponse zoomCreatedResponse = new ZoomUserCreatedResponse();

            //var jwtToken = _tokenHelper.CreateZoomToken();

            //var client = _httpClientFactory.CreateClient(APIName);
            //client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken.Token);

            //HttpContent content = JsonContent.Create(zoomCreateUserRequest);
            //HttpResponseMessage response = client.PostAsync(BASE_API_URL + "users", content).Result;

            //if (response.IsSuccessStatusCode)
            //{
            //    zoomCreatedResponse = JsonConvert.DeserializeObject<ZoomUserCreatedResponse>(response.Content.ReadAsStringAsync().Result);

            //    UserStudentDto userStudentDto = new UserStudentDto()
            //    {
            //        Active = true,
            //        AddedTime = DateTime.Now,
            //        ChangedTime = DateTime.Now,
            //        CitiesId = 107139,
            //        Deleted = false,
            //        Email = request.email,
            //        FirstName = request.first_name,
            //        LastName = request.last_name,
            //        GenderId = 1,
            //        NationalitysId = 150,
            //    };

            //    _ssoApi.AddUser(userStudentDto);
            //}
            //else
            //{
            //    return new ErrorDataResult<ZoomUserCreatedResponse>(zoomCreatedResponse, "Hata");
            //}

            //return new SuccessDataResult<ZoomUserCreatedResponse>(zoomCreatedResponse, "Oluşturuldu");
        }
    }
}
