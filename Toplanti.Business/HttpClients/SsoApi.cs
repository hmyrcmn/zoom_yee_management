using Core.Utilities.Results;
using Newtonsoft.Json;
using System.Net.Http;
using System.Reflection;
using Toplanti.Core.Utilities.Results;
using Toplanti.Entities.DTOs;

namespace Toplanti.Business.HttpClients
{
    public class SsoApi : ISsoApi
    {
        public string APIName = "SsoApi";
        private IHttpClientFactory _httpClientFactory;

        public SsoApi(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public UserStudentDto GetSsoUserInfoId(int ssoUserId)
        {
            UserStudentDto userStudentDto = new UserStudentDto();
            var client = _httpClientFactory.CreateClient(APIName);
            HttpResponseMessage response = client.GetAsync("api/class/ssouserinfoid?ssouserId=" + ssoUserId).Result;
            if (response.IsSuccessStatusCode)
            {
                userStudentDto = JsonConvert.DeserializeObject<UserStudentDto>(response.Content.ReadAsStringAsync().Result);
            }
            return userStudentDto;
        }

        public CenterPersonDTO Person(int userId)
        {
            CenterPersonDTO centerPersonDTO = new CenterPersonDTO();
            var client = _httpClientFactory.CreateClient(APIName);

            HttpResponseMessage response = client.GetAsync("api/Class/person?userId=" + userId).Result;
            if (response.IsSuccessStatusCode)
            {
                centerPersonDTO = JsonConvert.DeserializeObject<ApiResponseResult<CenterPersonDTO>>(response.Content.ReadAsStringAsync().Result).Data;
            }
            return centerPersonDTO;
        }

        public PersonDto GetUserOnlyEmail(string email)
        {
            PersonDto personDto = new PersonDto();
            var client = _httpClientFactory.CreateClient(APIName);

            HttpResponseMessage response = client.GetAsync("api/user/getuserbyemail?email=" + email).Result;
            if (response.IsSuccessStatusCode)
            {
                personDto = JsonConvert.DeserializeObject<PersonDto>(response.Content.ReadAsStringAsync().Result);
            }
            return personDto;
        }

        public PersonDto GetEmailByUserId(int id)
        {
            PersonDto personDto = new PersonDto();
            var client = _httpClientFactory.CreateClient(APIName);

            HttpResponseMessage response = client.GetAsync("api/user/getemailbyuserid?id=" + id).Result;
            if (response.IsSuccessStatusCode)
            {
                personDto = JsonConvert.DeserializeObject<PersonDto>(response.Content.ReadAsStringAsync().Result);
            }
            return personDto;
        }
    }
}

