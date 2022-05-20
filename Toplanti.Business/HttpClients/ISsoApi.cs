using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Toplanti.Entities.DTOs;

namespace Toplanti.Business.HttpClients
{
    public interface ISsoApi
    {
        public UserStudentDto GetSsoUserInfoId(int ssoUserId);
        public CenterPersonDTO Person(int userId);
        public PersonDto GetUserOnlyEmail(string email);
        public PersonDto GetEmailByUserId(int id);
    }
}
