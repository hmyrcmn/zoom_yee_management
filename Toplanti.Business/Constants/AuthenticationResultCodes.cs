namespace Toplanti.Business.Constants
{
    public static class AuthenticationResultCodes
    {
        public const string InvalidRequest = "AUTH_INVALID_REQUEST";
        public const string LdapInvalidCredentials = "LDAP_INVALID_CREDENTIALS";
        public const string LdapConnectionFailed = "LDAP_CONNECTION_FAILED";
        public const string LdapUserInfoNotFound = "LDAP_USER_INFO_NOT_FOUND";
        public const string LdapAuthenticated = "LDAP_AUTHENTICATED";
        public const string LdapUserProvisioned = "LDAP_USER_AUTOPROVISIONED";

        public const string OtpGenerated = "OTP_GENERATED";
        public const string OtpCooldown = "OTP_COOLDOWN";
        public const string OtpDeliveryFailed = "OTP_DELIVERY_FAILED";
        public const string OtpExpiredOrNotFound = "OTP_EXPIRED_OR_NOT_FOUND";
        public const string OtpInvalid = "OTP_INVALID";
        public const string OtpLocked = "OTP_LOCKED";
        public const string OtpVerified = "OTP_VERIFIED";
        public const string OtpUserProvisioned = "OTP_USER_AUTOPROVISIONED";

        public const string UnexpectedError = "AUTH_UNEXPECTED_ERROR";
    }
}
