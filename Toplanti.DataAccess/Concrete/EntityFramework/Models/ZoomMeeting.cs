using System;

namespace Toplanti.DataAccess.Concrete.EntityFramework.Models
{
    public class ZoomMeeting
    {
        public Guid MeetingId { get; set; }
        public Guid OwnerUserId { get; set; }
        public Guid? UserProvisioningId { get; set; }
        public string ZoomMeetingId { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string Agenda { get; set; } = string.Empty;
        public DateTime? StartTimeUtc { get; set; }
        public int? DurationMinutes { get; set; }
        public string? Timezone { get; set; }
        public string JoinUrl { get; set; } = string.Empty;
        public string StartUrl { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public AuthUser? OwnerUser { get; set; }
        public ZoomUserProvisioning? UserProvisioning { get; set; }
    }
}
