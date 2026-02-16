using Core.Utilities.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toplanti.Entities.DTOs;
using Core.Utilities.Security.JWT;

namespace Toplanti.Business.Abstract
{
    public interface IAuthService
    {
        IDataResult<UserStudentDto> UserInfo();
        IDataResult<AccessToken> Login(UserForLoginDto userForLoginDto);
    }
}
