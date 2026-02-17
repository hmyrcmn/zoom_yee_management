#nullable enable
using Toplanti.Entities.DTOs;

namespace Toplanti.Business.Abstract
{
    public interface ILdapService
    {
        bool ValidateUser(string username, string password);
        LdapUser? GetUserDetails(string username);
    }
}
