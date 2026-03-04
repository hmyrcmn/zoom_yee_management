using System;
using Toplanti.Core.Entities;

namespace Toplanti.Entities.DTOs.ZoomProvisioning
{
    public class CheckZoomAccountStatusRequest : IDto
    {
        public string Email { get; set; } = string.Empty;
        public Guid? ActorUserId { get; set; }
        public string IpAddress { get; set; } = string.Empty;
    }

    public class ProvisionZoomUserRequest : IDto
    {
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int UserType { get; set; } = 1;
        public Guid? ActorUserId { get; set; }
        public string IpAddress { get; set; } = string.Empty;
    }

    public class ZoomCallbackRequest : IDto
    {
        public string EventId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
    }

    public abstract class ZoomServiceResultBase : IDto
    {
        public bool Success { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class ZoomAccountStatusResult : ZoomServiceResultBase
    {
        public Guid? UserProvisioningId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public bool ExistsInLocalProvisioning { get; set; }
        public bool ExistsInZoomWorkspace { get; set; }
        public string ZoomUserId { get; set; } = string.Empty;
        public DateTime? LastSyncedAtUtc { get; set; }
    }

    public class ZoomProvisionUserResult : ZoomServiceResultBase
    {
        public Guid? UserProvisioningId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public string ZoomUserId { get; set; } = string.Empty;
        public int RetryAfterSeconds { get; set; }
    }

    public class ZoomCallbackResult : ZoomServiceResultBase
    {
        public Guid? WebhookInboxId { get; set; }
        public bool AlreadyProcessed { get; set; }
        public string EventType { get; set; } = string.Empty;
    }
}
