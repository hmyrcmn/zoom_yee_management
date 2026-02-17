using Toplanti.Core.Extensisons;
using Toplanti.Core.Utilities.IoC;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Core.Utilities.Helper
{
    public class UserCookie
    {
        private IHttpContextAccessor _httpContextAccessor;
        public int UserId()
        {
            _httpContextAccessor = ServiceTool.ServiceProvider.GetService<IHttpContextAccessor>();
            string userId = _httpContextAccessor.HttpContext.User.UserId();
            int convertUserId = Convert.ToInt32(userId);
            if (convertUserId == 0)
            {
                throw new UnauthorizedAccessException();
            }
            return convertUserId;
        }

        public string Rol()
        {
            _httpContextAccessor = ServiceTool.ServiceProvider.GetService<IHttpContextAccessor>();
            string roleName = _httpContextAccessor.HttpContext.User.RoleNameSingle();
            if (roleName == null)
            {
                throw new UnauthorizedAccessException();
            }
            return roleName;
        }

        public string Email()
        {
            _httpContextAccessor = ServiceTool.ServiceProvider.GetService<IHttpContextAccessor>();
            string email = _httpContextAccessor.HttpContext.User.Email();
            if (email == null)
            {
                throw new UnauthorizedAccessException();
            }
            return email;
        }

        public string FirstName()
        {
            _httpContextAccessor = ServiceTool.ServiceProvider.GetService<IHttpContextAccessor>();
            string firstName = _httpContextAccessor.HttpContext.User.FirstName();
            return firstName;
        }
        public string LastName()
        {
            _httpContextAccessor = ServiceTool.ServiceProvider.GetService<IHttpContextAccessor>();
            string lastName = _httpContextAccessor.HttpContext.User.LastName();
            return lastName;
        }
    }
}
