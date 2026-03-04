# Postman Setup (Auth + Zoom Integration)

## Files
- `Toplanti.AuthZoom.Integration.postman_collection.json`
- `Toplanti.AuthZoom.local.postman_environment.json`

## Import Order
1. Import the environment file.
2. Import the collection file.
3. Select `Toplanti Local (Auth+Zoom)` as active environment.

## Required Environment Values
- `baseUrl`: API URL (default: `http://localhost:5011`)
- `ldapUsernameOrEmail`, `ldapPassword`: internal LDAP test user
- `zoomEmail`, `zoomFirstName`, `zoomLastName`: provisioning target
- `webhookSecret`: must match `ZoomWebhook:SecretToken` in API config

## Execution Order
1. `1. Auth / LDAP Login`
2. `2. Zoom Provisioning / Get Status`
3. `2. Zoom Provisioning / Request Account (Provision)`
4. `3. Security / Webhook Invalid Signature (Expect 401)`
5. `3. Security / Webhook Valid Signature`

## Notes
- Collection auth uses `Bearer {{accessToken}}`.
- Login and OTP requests auto-save JWT into `accessToken`.
- Webhook valid-signature request computes `x-zm-signature` in pre-request script.
- `Verify OTP` endpoint is included, but OTP generation is service-level (no public controller endpoint yet).

## Optional DB Verification (Provisioning History)
After running `Request Account (Provision)`, verify transitions:

```sql
SELECT p.Email, p.ZoomStatusId, h.FromStatusId, h.ToStatusId, h.ActionType, h.CreatedAt
FROM zoom.UserProvisioning p
JOIN zoom.UserProvisioningHistory h ON h.UserProvisioningId = p.UserProvisioningId
WHERE p.EmailNormalized = UPPER('zoom.user@yee.org.tr')
ORDER BY h.CreatedAt;
```
