using Core.Utilities.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toplanti.Business.Abstract;
using Toplanti.Business.HttpClients;
using Toplanti.Core.Utilities.Helper;
using Toplanti.Entities.DTOs;

namespace Toplanti.Business.Concrete
{
    public class AuthManager : IAuthService
    {
        private ISsoApi _ssoApi;
        public AuthManager(ISsoApi ssoApi)
        {
            _ssoApi = ssoApi;
        }

        public IDataResult<UserStudentDto> UserInfo()
        {
            var userSsoId = new UserCookie().UserId();
            var userinfo = _ssoApi.GetSsoUserInfoId(userSsoId);
            return new SuccessDataResult<UserStudentDto>(userinfo, "Kullanıcı Bilgileri");
        }
    }
}
