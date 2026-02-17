using Toplanti.Core.Utilities.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toplanti.Business.Abstract;
using Toplanti.Business.HttpClients;
using Toplanti.Core.Utilities.Helper;
using Toplanti.Entities.DTOs;
using Toplanti.DataAccess.Abstract;
using Toplanti.Core.Utilities.Security.JWT;
using Toplanti.Core.Entities.Concrete;

namespace Toplanti.Business.Concrete
{
    public class AuthManager : IAuthService
    {
        private ISsoApi _ssoApi;
        private ILdapService _ldapService;
        private IUserDal _userDal;
        private ITokenHelper _tokenHelper;

        public AuthManager(ISsoApi ssoApi, ILdapService ldapService, IUserDal userDal, ITokenHelper tokenHelper)
        {
            _ssoApi = ssoApi;
            _ldapService = ldapService;
            _userDal = userDal;
            _tokenHelper = tokenHelper;
        }

        public IDataResult<UserStudentDto> UserInfo()
        {
            var userSsoId = new UserCookie().UserId();
            var userinfo = _ssoApi.GetSsoUserInfoId(userSsoId);
            return new SuccessDataResult<UserStudentDto>(userinfo, "Kullanıcı Bilgileri");
        }

        public IDataResult<AccessToken> Login(UserForLoginDto userForLoginDto)
        {
            // 1. LDAP Validation
            if (!_ldapService.ValidateUser(userForLoginDto.Email, userForLoginDto.Password))
            {
                return new ErrorDataResult<AccessToken>("Kullanıcı adı veya şifre hatalı");
            }

            // 2. Sync Logic (Check Local DB)
            var username = userForLoginDto.Email.Split('@')[0]; 
            var ldapUser = _ldapService.GetUserDetails(username);

            if (ldapUser == null)
            {
                 return new ErrorDataResult<AccessToken>("LDAP kullanıcı bilgileri alınamadı.");
            }

            var userToCheck = _userDal.Get(u => u.Email == ldapUser.Email);

            if (userToCheck == null)
            {
                // Create New User
                var newUser = new Toplanti.Core.Entities.Concrete.User
                {
                    Username = ldapUser.Username ?? string.Empty,
                    FirstName = ldapUser.Name ?? string.Empty,
                    LastName = ldapUser.Surname ?? string.Empty,
                    Email = ldapUser.Email ?? string.Empty,
                    Status = true,
                    AddedTime = DateTime.Now,
                    Active = true,
                    Deleted = false,
                    PasswordHash = new byte[0], 
                    PasswordSalt = new byte[0]
                };
                _userDal.Add(newUser);
                userToCheck = _userDal.Get(u => u.Email == ldapUser.Email);
            }
            else
            {
                // Update Existing User
                userToCheck.FirstName = ldapUser.Name ?? userToCheck.FirstName;
                userToCheck.LastName = ldapUser.Surname ?? userToCheck.LastName;
                _userDal.Update(userToCheck);
            }

            // 3. Generate Token
            var accessToken = _tokenHelper.CreateToken(userToCheck!, new List<Toplanti.Core.Entities.Concrete.OperationClaim>());
            
            return new SuccessDataResult<AccessToken>(accessToken, "Giriş başarılı");
        }
    }
}
