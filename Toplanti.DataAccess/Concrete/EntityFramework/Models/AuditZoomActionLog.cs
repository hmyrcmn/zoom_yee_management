using System;

namespace Toplanti.DataAccess.Concrete.EntityFramework.Models
{
    public class AuditZoomActionLog
    {
        public long AuditZoomActionLogId { get; set; }
        public Guid? ActorUserId { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string? TargetEmail { get; set; }
        public string? TargetMeetingId { get; set; }
        public string? RequestIpAddress { get; set; }
        public string ResultCode { get; set; } = string.Empty;
        public string? Message { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
