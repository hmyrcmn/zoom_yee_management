namespace Toplanti.Business.Constants
{
    public static class AuthFlowCodes
    {
        public const string Prefix = "AUTHFLOW:";
        public const string Delimiter = "|";

        public const string OtpRequired = "OTP_REQUIRED";
        public const string OtpInvalid = "OTP_INVALID";
        public const string OtpDeliveryFailed = "OTP_DELIVERY_FAILED";

        public const string ZoomActivationPending = "ZOOM_ACTIVATION_PENDING";
        public const string BilisimContactRequired = "BILISIM_CONTACT_REQUIRED";
        public const string ZoomValidationFailed = "ZOOM_VALIDATION_FAILED";
        public const string ZoomAutoProvisionFailed = "ZOOM_AUTO_PROVISION_FAILED";
    }
}
