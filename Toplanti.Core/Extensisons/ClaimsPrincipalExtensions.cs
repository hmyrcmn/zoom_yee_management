using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Core.Extensisons
{
    public static class ClaimsPrincipalExtensions
    {
        public static List<string> Claims(this ClaimsPrincipal claimsPrincipal, string claimType)
        {
            var results = claimsPrincipal?.FindAll(claimType)?.Select(x => x.Value).ToList();
            return results ?? new List<string>();
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

        public static string Email(this ClaimsPrincipal claimsPrincipal)
        {
            return claimsPrincipal?.FindAll("email").Select(x => x.Value).ToList().FirstOrDefault();
        }
        public static string FirstName(this ClaimsPrincipal claimsPrincipal)
        {
            var result = claimsPrincipal?.FindAll("firstName")?.Select(x => x.Value).FirstOrDefault();
            return result;
        }

        public static string LastName(this ClaimsPrincipal claimsPrincipal)
        {
            var result = claimsPrincipal?.FindAll("lastName")?.Select(x => x.Value).FirstOrDefault();
            return result;
        }
    }
}
