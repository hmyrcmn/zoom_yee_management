#nullable enable
using Toplanti.Entities.DTOs;

namespace Toplanti.Business.Abstract
{
    public interface ILdapService
    {
        LdapUser? ValidateUser(string username, string password);
    }
}
