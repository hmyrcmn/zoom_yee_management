using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Toplanti.DataAccess.Concrete.EntityFramework.Contexts;

namespace Toplanti.WebAPI.Infrastructure
{
    internal static class DatabaseBootstrapper
    {
        private static readonly string[] SqlScriptNames =
        {
            "auth-module-schema.sql",
            "zoom-provisioning-schema.sql"
        };

        private static readonly Regex GoBatchSeparator = new(
            @"^\s*GO\s*(?:--.*)?$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static async Task BootstrapAsync(
            ToplantiContext context,
            string contentRootPath,
            CancellationToken cancellationToken = default)
        {
            context.Database.EnsureCreated();

            foreach (var scriptName in SqlScriptNames)
            {
                var scriptPath = ResolveScriptPath(contentRootPath, scriptName);
                if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
                {
                    continue;
                }

                try
                {
                    await ExecuteSqlScriptAsync(context, scriptPath, cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: SQL bootstrap script failed ({scriptName}) - {ex.Message}");
                }
            }

            await ZoomStateMachineSeeder.SeedIfRequiredAsync(context, cancellationToken);
        }

        private static async Task ExecuteSqlScriptAsync(
            ToplantiContext context,
            string scriptPath,
            CancellationToken cancellationToken)
        {
            var scriptContent = await File.ReadAllTextAsync(scriptPath, cancellationToken);
            var batches = GoBatchSeparator.Split(scriptContent);

            foreach (var batch in batches)
            {
                var sql = (batch ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(sql))
                {
                    continue;
                }

                await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            }
        }

        private static string ResolveScriptPath(string contentRootPath, string scriptName)
        {
            var candidates = new[]
            {
                Path.Combine(contentRootPath, "..", "docs", scriptName),
                Path.Combine(contentRootPath, "docs", scriptName),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "docs", scriptName)
            };

            foreach (var candidate in candidates)
            {
                var fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return string.Empty;
        }
    }
}
