using Microsoft.EntityFrameworkCore;
using Toplanti.DataAccess.Concrete.EntityFramework.Contexts;
using Toplanti.DataAccess.Concrete.EntityFramework.Models;
using Toplanti.Entities.Enums;

namespace Toplanti.WebAPI.Infrastructure
{
    internal static class ZoomStateMachineSeeder
    {
        private static readonly ZoomStatus[] RequiredStatuses =
        {
            new()
            {
                ZoomStatusId = (byte)ZoomProvisioningStatus.None,
                Name = "None",
                DisplayName = "None",
                IsTerminal = false,
                IsActive = true
            },
            new()
            {
                ZoomStatusId = (byte)ZoomProvisioningStatus.ProvisioningPending,
                Name = "ProvisioningPending",
                DisplayName = "Provisioning Pending",
                IsTerminal = false,
                IsActive = true
            },
            new()
            {
                ZoomStatusId = (byte)ZoomProvisioningStatus.ActivationPending,
                Name = "ActivationPending",
                DisplayName = "Activation Pending",
                IsTerminal = false,
                IsActive = true
            },
            new()
            {
                ZoomStatusId = (byte)ZoomProvisioningStatus.Active,
                Name = "Active",
                DisplayName = "Active",
                IsTerminal = false,
                IsActive = true
            },
            new()
            {
                ZoomStatusId = (byte)ZoomProvisioningStatus.Failed,
                Name = "Failed",
                DisplayName = "Failed",
                IsTerminal = false,
                IsActive = true
            },
            new()
            {
                ZoomStatusId = (byte)ZoomProvisioningStatus.ManualSupportRequired,
                Name = "ManualSupportRequired",
                DisplayName = "Manual Support Required",
                IsTerminal = true,
                IsActive = true
            }
        };

        private static readonly (byte FromStatusId, byte ToStatusId, string ActionType, string Description)[] RequiredTransitions =
        {
            ((byte)ZoomProvisioningStatus.None, (byte)ZoomProvisioningStatus.ProvisioningPending, "PROVISION", "Initial provisioning request."),
            ((byte)ZoomProvisioningStatus.Failed, (byte)ZoomProvisioningStatus.ProvisioningPending, "RETRY_PROVISION", "Retry after failure."),
            ((byte)ZoomProvisioningStatus.ProvisioningPending, (byte)ZoomProvisioningStatus.ActivationPending, "API_ACCEPTED", "Zoom accepted autoCreate."),
            ((byte)ZoomProvisioningStatus.ProvisioningPending, (byte)ZoomProvisioningStatus.Active, "API_CONFLICT", "Zoom returned conflict; account exists."),
            ((byte)ZoomProvisioningStatus.ProvisioningPending, (byte)ZoomProvisioningStatus.Failed, "API_RATE_LIMIT", "Zoom rate limit hit."),
            ((byte)ZoomProvisioningStatus.ProvisioningPending, (byte)ZoomProvisioningStatus.Failed, "API_ERROR", "Zoom API error."),
            ((byte)ZoomProvisioningStatus.ActivationPending, (byte)ZoomProvisioningStatus.Active, "WEBHOOK_USER_ACTIVATED", "Webhook confirms activation."),
            ((byte)ZoomProvisioningStatus.None, (byte)ZoomProvisioningStatus.Active, "SYNC_EXTERNAL_LOOKUP", "External lookup found active account."),
            ((byte)ZoomProvisioningStatus.None, (byte)ZoomProvisioningStatus.Active, "WEBHOOK_DISCOVERY", "Webhook discovered active account."),
            ((byte)ZoomProvisioningStatus.Failed, (byte)ZoomProvisioningStatus.ManualSupportRequired, "MANUAL_SUPPORT_ESCALATE", "Escalate failed provisioning to IT."),
            ((byte)ZoomProvisioningStatus.ActivationPending, (byte)ZoomProvisioningStatus.ManualSupportRequired, "MANUAL_SUPPORT_ESCALATE", "Activation timeout escalation.")
        };

        public static async Task SeedIfRequiredAsync(ToplantiContext context, CancellationToken cancellationToken = default)
        {
            if (await context.ZoomStatuses.AsNoTracking().AnyAsync(cancellationToken))
            {
                return;
            }

            await context.ZoomStatuses.AddRangeAsync(RequiredStatuses, cancellationToken);

            var transitionRows = RequiredTransitions
                .Select(transition => new ZoomStatusTransitionRule
                {
                    FromStatusId = transition.FromStatusId,
                    ToStatusId = transition.ToStatusId,
                    ActionType = transition.ActionType,
                    Description = transition.Description,
                    IsEnabled = true
                })
                .ToList();

            await context.ZoomStatusTransitionRules.AddRangeAsync(transitionRows, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
