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
            var httpContext = _httpContextAccessor?.HttpContext;
            var roleClaims = httpContext?.User?.ClaimRoles() ?? new System.Collections.Generic.List<string>();
            var department = httpContext?.User?.Department();
            foreach (var role in _roles)
            {
                var normalizedRole = role?.Trim() ?? string.Empty;

                // Admin access is strictly bound to Bilişim department.
                if (normalizedRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                {
                    if (IsBilisimDepartment(department))
                    {
                        return;
                    }
                    continue;
                }

                if (roleClaims.Any(rc => string.Equals(rc, normalizedRole, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }
            }

            var userId = httpContext?.User?.UserId() ?? "unknown";
            var path = httpContext?.Request?.Path.Value ?? "unknown";
            Console.WriteLine(
                $"[SecuredOperation] Authorization denied. RequiredRoles={string.Join(",", _roles)} UserId={userId} UserRoles={string.Join(",", roleClaims)} Department={department ?? string.Empty} Path={path}");

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
