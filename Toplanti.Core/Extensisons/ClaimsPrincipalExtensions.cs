using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Core.Extensisons
{
    public static class ClaimsPrincipalExtensions
    {
        public static List<string> Claims(this ClaimsPrincipal claimsPrincipal, string claimType)
        {
            var role = claimsPrincipal?.FindAll(claimType)?.Select(x => x.Value).ToList();
            if (role.Count > 0)
            {
                var result = role[0].Split(",").ToList();
                return result;
            }
            else
            {
                return role;
            }
        }

        public static List<string> ClaimRoles(this ClaimsPrincipal claimsPrincipal)
        {
            return claimsPrincipal?.Claims(ClaimTypes.Role);
        }

        public static string User(this ClaimsPrincipal claimsPrincipal, string claimType)
        {
            var result = claimsPrincipal?.FindAll(claimType)?.Select(x => x.Value).FirstOrDefault();
            return result;
        }

        public static string UserId(this ClaimsPrincipal claimsPrincipal)
        {
            return claimsPrincipal?.User(ClaimTypes.NameIdentifier);
        }

        public static string RoleName(this ClaimsPrincipal claimsPrincipal, string claimType)
        {
            var result = claimsPrincipal?.FindAll(claimType)?.Select(x => x.Value).FirstOrDefault();
            return result;
        }

        public static string RoleNameSingle(this ClaimsPrincipal claimsPrincipal)
        {
            return claimsPrincipal?.RoleName(ClaimTypes.Name);
        }
    }
}
