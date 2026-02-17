using System;

namespace Toplanti.Core.Entities.Concrete
{
    public class LdapSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string BaseDn { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
    }
}
