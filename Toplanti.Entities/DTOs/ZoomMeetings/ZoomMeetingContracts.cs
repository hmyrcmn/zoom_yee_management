using System;
using System.Collections.Generic;
using Toplanti.Core.Entities;

namespace Toplanti.Entities.DTOs.ZoomMeetings
{
    public class CreateZoomMeetingRequest : IDto
    {
        public string Topic { get; set; } = string.Empty;
        public string Agenda { get; set; } = string.Empty;
        public DateTime StartTimeUtc { get; set; }
        public int DurationMinutes { get; set; } = 30;
        public string Timezone { get; set; } = "UTC";
    }

    public class ZoomMeetingSummary : IDto
    {
        public Guid MeetingId { get; set; }
        public string ZoomMeetingId { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string Agenda { get; set; } = string.Empty;
        public DateTime? StartTimeUtc { get; set; }
        public int? DurationMinutes { get; set; }
        public string Timezone { get; set; } = string.Empty;
        public string JoinUrl { get; set; } = string.Empty;
        public string StartUrl { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }

    public class ZoomMeetingOperationResult : IDto
    {
        public bool Success { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public ZoomMeetingSummary? Meeting { get; set; }
    }

    public class ZoomMeetingHistoryResult : IDto
    {
        public bool Success { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public IReadOnlyList<ZoomMeetingSummary> Meetings { get; set; } = Array.Empty<ZoomMeetingSummary>();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }
}
