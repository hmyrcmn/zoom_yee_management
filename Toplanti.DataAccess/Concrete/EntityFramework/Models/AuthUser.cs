using System;
using System.Collections.Generic;

namespace Toplanti.DataAccess.Concrete.EntityFramework.Models
{
    public class AuthUser
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string EmailNormalized { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? Department { get; set; }
        public bool IsInternal { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public ICollection<AuthOtpChallenge> OtpChallenges { get; set; } = new List<AuthOtpChallenge>();
        public ICollection<ZoomUserProvisioning> ZoomUserProvisionings { get; set; } = new List<ZoomUserProvisioning>();
        public ICollection<ZoomMeeting> OwnedZoomMeetings { get; set; } = new List<ZoomMeeting>();
    }
}
