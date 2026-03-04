namespace Toplanti.Entities.Enums
{
    /// <summary>
    /// Local status catalog for Zoom provisioning lifecycle.
    /// Stored in zoom.ZoomStatus table.
    /// </summary>
    public enum ZoomProvisioningStatus : byte
    {
        None = 0,
        ProvisioningPending = 1,
        ActivationPending = 2,
        Active = 3,
        Failed = 4,
        ManualSupportRequired = 5
    }
}
