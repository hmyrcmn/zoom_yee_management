using System;
using System.Threading;
using System.Threading.Tasks;
using Toplanti.Entities.DTOs.ZoomMeetings;

namespace Toplanti.Business.Abstract
{
    /// <summary>
    /// Provides object-level secured meeting operations for Zoom meetings.
    /// </summary>
    public interface IZoomMeetingService
    {
        /// <summary>
        /// Creates a Zoom meeting on behalf of the actor and stores ownership in local database.
        /// </summary>
        Task<ZoomMeetingOperationResult> CreateMeetingAsync(
            Guid actorUserId,
            CreateZoomMeetingRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns only meetings owned by actor user.
        /// </summary>
        Task<ZoomMeetingHistoryResult> GetHistoryAsync(
            Guid actorUserId,
            int pageNumber = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a meeting only if the actor owns it.
        /// </summary>
        Task<ZoomMeetingOperationResult> GetMeetingByIdAsync(
            Guid actorUserId,
            Guid meetingId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a meeting only if the actor owns it.
        /// </summary>
        Task<ZoomMeetingOperationResult> DeleteMeetingAsync(
            Guid actorUserId,
            Guid meetingId,
            CancellationToken cancellationToken = default);
    }
}
