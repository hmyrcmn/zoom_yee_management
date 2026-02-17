#nullable enable
using Toplanti.Core.Entities;

namespace Toplanti.Entities.DTOs
{
    public class LdapUser : IDto
    {
        public string Name { get; set; } = string.Empty;
        public string Surname { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }
}
