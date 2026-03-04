using System;
using System.Collections.Generic;

namespace Toplanti.DataAccess.Concrete.EntityFramework.Models
{
    public class ZoomUserProvisioning
    {
        public Guid UserProvisioningId { get; set; }
        public Guid? UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string EmailNormalized { get; set; } = string.Empty;
        public string ZoomUserId { get; set; } = string.Empty;
        public byte ZoomStatusId { get; set; }
        public string? LastErrorCode { get; set; }
        public string? LastErrorMessage { get; set; }
        public DateTime? LastSyncedAt { get; set; }
        public Guid? CorrelationId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public AuthUser? User { get; set; }
        public ZoomStatus? ZoomStatus { get; set; }
        public ICollection<ZoomUserProvisioningHistory> History { get; set; } = new List<ZoomUserProvisioningHistory>();
    }
}
