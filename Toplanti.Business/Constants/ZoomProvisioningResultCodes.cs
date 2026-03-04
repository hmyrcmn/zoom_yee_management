namespace Toplanti.Business.Constants
{
    public static class ZoomProvisioningResultCodes
    {
        public const string InvalidRequest = "ZOOM_PROVISIONING_INVALID_REQUEST";
        public const string StatusFetched = "ZOOM_STATUS_FETCHED";
        public const string ProvisioningStarted = "ZOOM_PROVISIONING_STARTED";
        public const string ProvisioningConflictActivated = "ZOOM_PROVISIONING_CONFLICT_ACTIVE";
        public const string ProvisioningRateLimited = "ZOOM_PROVISIONING_RATE_LIMITED";
        public const string ProvisioningFailed = "ZOOM_PROVISIONING_FAILED";
        public const string CallbackProcessed = "ZOOM_CALLBACK_PROCESSED";
        public const string CallbackAlreadyProcessed = "ZOOM_CALLBACK_ALREADY_PROCESSED";
        public const string CallbackInvalidSignature = "ZOOM_CALLBACK_INVALID_SIGNATURE";
        public const string TransitionNotAllowed = "ZOOM_TRANSITION_NOT_ALLOWED";
        public const string CatalogNotInitialized = "ZOOM_STATUS_CATALOG_NOT_INITIALIZED";
        public const string UnexpectedError = "ZOOM_PROVISIONING_UNEXPECTED_ERROR";
    }
}
