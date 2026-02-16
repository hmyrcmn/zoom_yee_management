using System;

namespace Toplanti.Core.Entities.Concrete
{
    public class LdapSettings
    {
        public string? Host { get; set; }
        public int Port { get; set; }
        public string? BaseDn { get; set; }
        public string? Domain { get; set; }
    }
}
