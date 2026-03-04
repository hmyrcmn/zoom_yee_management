using System.Threading;
using System.Threading.Tasks;
using Toplanti.Entities.DTOs.ZoomProvisioning;

namespace Toplanti.Business.Abstract
{
    /// <summary>
    /// Coordinates Zoom account provisioning lifecycle and webhook callbacks.
    /// </summary>
    public interface IZoomProvisioningService
    {
        /// <summary>
        /// Returns current provisioning status for an email and synchronizes local state with Zoom when needed.
        /// </summary>
        Task<ZoomAccountStatusResult> CheckAccountStatusAsync(
            CheckZoomAccountStatusRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts or continues provisioning flow according to configured status transition rules.
        /// </summary>
        Task<ZoomProvisionUserResult> ProvisionUserAsync(
            ProvisionZoomUserRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Handles Zoom webhook callback with signature verification and idempotency.
        /// </summary>
        Task<ZoomCallbackResult> HandleCallbackAsync(
            ZoomCallbackRequest request,
            CancellationToken cancellationToken = default);
    }
}
