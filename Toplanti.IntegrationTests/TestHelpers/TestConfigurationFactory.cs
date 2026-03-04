using Microsoft.Extensions.Configuration;

namespace Toplanti.IntegrationTests.TestHelpers
{
    internal static class TestConfigurationFactory
    {
        public static IConfiguration Create(params (string Key, string Value)[] values)
        {
            var pairs = values.ToDictionary(x => x.Key, x => x.Value);
            return new ConfigurationBuilder()
                .AddInMemoryCollection(pairs!)
                .Build();
        }
    }
}
