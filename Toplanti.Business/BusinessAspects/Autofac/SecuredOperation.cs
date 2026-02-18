using Castle.DynamicProxy;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Toplanti.Business.Constants;
using Toplanti.Core.Utilities.Interceptors;
using Toplanti.Core.Utilities.IoC;
using Toplanti.Core.Extensisons;

namespace Toplanti.Business.BusinessAspects.Autofac
{
    public class SecuredOperation : MethodInterception
    {
        private string[] _roles;
        private IHttpContextAccessor _httpContextAccessor;

        public SecuredOperation(string roles)
        {
            _roles = roles.Split(',');
            _httpContextAccessor = ServiceTool.ServiceProvider.GetService<IHttpContextAccessor>();

        }

        protected override void OnBefore(IInvocation invocation)
        {
            var roleClaims = _httpContextAccessor.HttpContext.User.ClaimRoles();
            var department = _httpContextAccessor.HttpContext.User.Department();
            foreach (var role in _roles)
            {
                if (roleClaims.Contains(role))
                {
                    return;
                }

                if (role.Trim().Equals("Admin", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(department, "Bilişim", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            throw new UnauthorizedAccessException(Messages.AuthorizationDenied);
        }
    }
}
