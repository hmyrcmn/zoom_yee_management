using Castle.DynamicProxy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using Toplanti.Business.Constants;
using Toplanti.Core.Extensisons;
using Toplanti.Core.Utilities.Interceptors;
using Toplanti.Core.Utilities.IoC;

namespace Toplanti.Business.BusinessAspects.Autofac
{
    public class SecuredOperation : MethodInterception
    {
        private readonly string[] _roles;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SecuredOperation(string roles)
        {
            _roles = (roles ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            _httpContextAccessor = ServiceTool.ServiceProvider.GetService<IHttpContextAccessor>();
        }

        protected override void OnBefore(IInvocation invocation)
        {
            var httpContext = _httpContextAccessor?.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated != true)
            {
                throw new UnauthorizedAccessException("Authentication is required.");
            }

            var roleClaims = httpContext.User.ClaimRoles() ?? new System.Collections.Generic.List<string>();
            foreach (var requiredRole in _roles)
            {
                if (HasRequiredRole(requiredRole, roleClaims))
                {
                    return;
                }
            }

            var userId = httpContext.User.UserId() ?? "unknown";
            var path = httpContext.Request?.Path.Value ?? "unknown";
            Console.WriteLine(
                $"[SecuredOperation] Authorization denied. RequiredRoles={string.Join(",", _roles)} UserId={userId} UserRoles={string.Join(",", roleClaims)} Path={path}");

            throw new UnauthorizedAccessException(Messages.AuthorizationDenied);
        }

        private static bool HasRequiredRole(string requiredRole, System.Collections.Generic.IReadOnlyCollection<string> roleClaims)
        {
            if (string.IsNullOrWhiteSpace(requiredRole))
            {
                return false;
            }

            if (requiredRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                // IT users are allowed on admin-protected flows.
                return roleClaims.Any(rc => rc.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                    || roleClaims.Any(rc => rc.Equals("IT", StringComparison.OrdinalIgnoreCase));
            }

            if (requiredRole.Equals("IT", StringComparison.OrdinalIgnoreCase))
            {
                return roleClaims.Any(rc => rc.Equals("IT", StringComparison.OrdinalIgnoreCase))
                    || roleClaims.Any(rc => rc.Equals("Admin", StringComparison.OrdinalIgnoreCase));
            }

            return roleClaims.Any(rc => rc.Equals(requiredRole, StringComparison.OrdinalIgnoreCase));
        }
    }
}
