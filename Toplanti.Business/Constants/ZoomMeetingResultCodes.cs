namespace Toplanti.Business.Constants
{
    public static class ZoomMeetingResultCodes
    {
        public const string InvalidRequest = "ZOOM_MEETING_INVALID_REQUEST";
        public const string MeetingDuplicate = "ZOOM_MEETING_DUPLICATE";
        public const string MeetingCreated = "ZOOM_MEETING_CREATED";
        public const string MeetingDeleted = "ZOOM_MEETING_DELETED";
        public const string MeetingHistoryFetched = "ZOOM_MEETING_HISTORY_FETCHED";
        public const string MeetingFetched = "ZOOM_MEETING_FETCHED";
        public const string MeetingNotFoundOrForbidden = "ZOOM_MEETING_NOT_FOUND_OR_FORBIDDEN";
        public const string MeetingCreateFailed = "ZOOM_MEETING_CREATE_FAILED";
        public const string MeetingDeleteFailed = "ZOOM_MEETING_DELETE_FAILED";
        public const string UnexpectedError = "ZOOM_MEETING_UNEXPECTED_ERROR";
    }
}
