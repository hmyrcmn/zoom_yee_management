using Core.Entities;

namespace Toplanti.Entities.DTOs
{
    public class LdapUser : IDto
    {
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public string? Email { get; set; }
        public string? Username { get; set; }
    }
}
