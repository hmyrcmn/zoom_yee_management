using Toplanti.Business.Abstract;
using Toplanti.Entities.DTOs.ZoomProvisioning;

namespace Toplanti.IntegrationTests.TestHelpers
{
    internal sealed class MockZoomProvisioningService : IZoomProvisioningService
    {
        public Func<CheckZoomAccountStatusRequest, ZoomAccountStatusResult> CheckStatusHandler { get; set; } =
            _ => new ZoomAccountStatusResult
            {
                Success = true,
                Code = "MOCK_STATUS",
                Message = "ok",
                StatusName = "Active"
            };

        public Func<ProvisionZoomUserRequest, ZoomProvisionUserResult> ProvisionUserHandler { get; set; } =
            _ => new ZoomProvisionUserResult
            {
                Success = true,
                Code = "MOCK_PROVISION",
                Message = "ok",
                StatusName = "ActivationPending"
            };

        public Func<ZoomCallbackRequest, ZoomCallbackResult> CallbackHandler { get; set; } =
            _ => new ZoomCallbackResult
            {
                Success = true,
                Code = "MOCK_CALLBACK",
                Message = "ok"
            };

        public Task<ZoomAccountStatusResult> CheckAccountStatusAsync(
            CheckZoomAccountStatusRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CheckStatusHandler(request));
        }

        public Task<ZoomProvisionUserResult> ProvisionUserAsync(
            ProvisionZoomUserRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ProvisionUserHandler(request));
        }

        public Task<ZoomCallbackResult> HandleCallbackAsync(
            ZoomCallbackRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CallbackHandler(request));
        }
    }
}
