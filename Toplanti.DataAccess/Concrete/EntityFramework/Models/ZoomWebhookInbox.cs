using System;

namespace Toplanti.DataAccess.Concrete.EntityFramework.Models
{
    public class ZoomWebhookInbox
    {
        public Guid WebhookInboxId { get; set; }
        public string EventId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string PayloadHash { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public string? RequestIpAddress { get; set; }
        public string? ProcessingResult { get; set; }
        public DateTime ReceivedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public Guid? CorrelationId { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
