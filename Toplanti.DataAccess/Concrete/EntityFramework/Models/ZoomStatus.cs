using System.Collections.Generic;

namespace Toplanti.DataAccess.Concrete.EntityFramework.Models
{
    public class ZoomStatus
    {
        public byte ZoomStatusId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsTerminal { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<ZoomStatusTransitionRule> OutgoingTransitions { get; set; } = new List<ZoomStatusTransitionRule>();
        public ICollection<ZoomStatusTransitionRule> IncomingTransitions { get; set; } = new List<ZoomStatusTransitionRule>();
        public ICollection<ZoomUserProvisioning> UserProvisionings { get; set; } = new List<ZoomUserProvisioning>();
    }
}
