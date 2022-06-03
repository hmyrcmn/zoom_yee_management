using Core.Extensisons;
using Core.Utilities.IoC;
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
            int convertUserId = Convert.ToInt16(userId);
            return convertUserId;
        }

        public string Rol()
        {
            _httpContextAccessor = ServiceTool.ServiceProvider.GetService<IHttpContextAccessor>();
            string roleName = _httpContextAccessor.HttpContext.User.RoleNameSingle();
            return roleName;
        }

        public string Email()
        {
            _httpContextAccessor = ServiceTool.ServiceProvider.GetService<IHttpContextAccessor>();
            string email = _httpContextAccessor.HttpContext.User.Email();
            return email;
        }
    }
}
