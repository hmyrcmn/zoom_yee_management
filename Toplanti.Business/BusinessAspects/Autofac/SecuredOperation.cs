using Castle.DynamicProxy;
using Microsoft.AspNetCore.Http;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
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
                if (roleClaims.Any(rc => string.Equals(rc, role, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                if (role.Trim().Equals("Admin", StringComparison.OrdinalIgnoreCase) &&
                    IsBilisimDepartment(department))
                {
                    return;
                }
            }
            throw new UnauthorizedAccessException(Messages.AuthorizationDenied);
        }

        private static bool IsBilisimDepartment(string department)
        {
            if (string.IsNullOrWhiteSpace(department))
            {
                return false;
            }

            var normalized = department
                .Trim()
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormD);

            var chars = normalized
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .Select(c => c == 'ı' ? 'i' : c)
                .ToArray();

            return new string(chars) == "bilisim";
        }
    }
}
