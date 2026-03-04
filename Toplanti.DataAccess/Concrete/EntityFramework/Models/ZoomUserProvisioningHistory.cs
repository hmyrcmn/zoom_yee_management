using System;

namespace Toplanti.DataAccess.Concrete.EntityFramework.Models
{
    public class ZoomUserProvisioningHistory
    {
        public Guid UserProvisioningHistoryId { get; set; }
        public Guid UserProvisioningId { get; set; }
        public byte? FromStatusId { get; set; }
        public byte ToStatusId { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public Guid? ActorUserId { get; set; }
        public string Source { get; set; } = string.Empty;
        public int? HttpStatusCode { get; set; }
        public string? Message { get; set; }
        public string? RawResponse { get; set; }
        public string? RequestIpAddress { get; set; }
        public Guid? CorrelationId { get; set; }
        public DateTime CreatedAt { get; set; }

        public ZoomUserProvisioning? UserProvisioning { get; set; }
        public ZoomStatus? FromStatus { get; set; }
        public ZoomStatus? ToStatus { get; set; }
    }
}
