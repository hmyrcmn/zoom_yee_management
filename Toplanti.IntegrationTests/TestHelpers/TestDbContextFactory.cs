using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Toplanti.DataAccess.Concrete.EntityFramework.Contexts;

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
            return context;
        }
    }
}
