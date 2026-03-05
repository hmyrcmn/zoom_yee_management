using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Toplanti.DataAccess.Concrete.EntityFramework.Contexts;
using Toplanti.DataAccess.Concrete.EntityFramework.Models;
using Toplanti.Entities.Enums;

namespace Toplanti.IntegrationTests.TestHelpers
{
    internal static class TestDbContextFactory
    {
        public static ToplantiContext CreateContext(string? databaseName = null)
        {
            var dbName = string.IsNullOrWhiteSpace(databaseName)
                ? Guid.NewGuid().ToString("N")
                : databaseName.Trim();

            var options = new DbContextOptionsBuilder<ToplantiContext>()
                .UseInMemoryDatabase(dbName)
                .EnableSensitiveDataLogging()
                .Options;

            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
            var context = new ToplantiContext(options, configuration);
            context.Database.EnsureCreated();
            SeedZoomStatuses(context);
            return context;
        }

        private static void SeedZoomStatuses(ToplantiContext context)
        {
            if (context.ZoomStatuses.Any())
            {
                return;
            }

            context.ZoomStatuses.AddRange(
                new ZoomStatus
                {
                    ZoomStatusId = (byte)ZoomProvisioningStatus.None,
                    Name = "None",
                    DisplayName = "Not Provisioned",
                    IsTerminal = false,
                    IsActive = true
                },
                new ZoomStatus
                {
                    ZoomStatusId = (byte)ZoomProvisioningStatus.ProvisioningPending,
                    Name = "ProvisioningPending",
                    DisplayName = "Provisioning Pending",
                    IsTerminal = false,
                    IsActive = true
                },
                new ZoomStatus
                {
                    ZoomStatusId = (byte)ZoomProvisioningStatus.ActivationPending,
                    Name = "ActivationPending",
                    DisplayName = "Activation Pending",
                    IsTerminal = false,
                    IsActive = true
                },
                new ZoomStatus
                {
                    ZoomStatusId = (byte)ZoomProvisioningStatus.Active,
                    Name = "Active",
                    DisplayName = "Active",
                    IsTerminal = true,
                    IsActive = true
                },
                new ZoomStatus
                {
                    ZoomStatusId = (byte)ZoomProvisioningStatus.Failed,
                    Name = "Failed",
                    DisplayName = "Failed",
                    IsTerminal = true,
                    IsActive = true
                },
                new ZoomStatus
                {
                    ZoomStatusId = (byte)ZoomProvisioningStatus.ManualSupportRequired,
                    Name = "ManualSupportRequired",
                    DisplayName = "Manual Support Required",
                    IsTerminal = true,
                    IsActive = true
                });

            context.SaveChanges();
        }
    }
}
