using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using Toplanti.DataAccess.Concrete.EntityFramework.Contexts;

namespace Toplanti.WebAPI.Services
{
    public class ZoomAdminAuditService : IZoomAdminAuditService
    {
        private readonly ToplantiContext _context;

        public ZoomAdminAuditService(ToplantiContext context)
        {
            _context = context;
        }

        public async Task LogAsync(string? adminEmail, string actionType, string? targetEmails, string result, string? message)
        {
            try
            {
                await EnsureTableAsync();

                const string insertSql = @"
INSERT INTO dbo.ZoomAdminOperationLogs (AdminEmail, ActionType, TargetEmails, [Timestamp], Result, Message)
VALUES (@AdminEmail, @ActionType, @TargetEmails, SYSUTCDATETIME(), @Result, @Message)";

                var parameters = new[]
                {
                    new SqlParameter("@AdminEmail", (object?)Normalize(adminEmail) ?? DBNull.Value),
                    new SqlParameter("@ActionType", Normalize(actionType) ?? string.Empty),
                    new SqlParameter("@TargetEmails", (object?)Normalize(targetEmails) ?? DBNull.Value),
                    new SqlParameter("@Result", Normalize(result) ?? string.Empty),
                    new SqlParameter("@Message", (object?)Normalize(message) ?? DBNull.Value),
                };

                await _context.Database.ExecuteSqlRawAsync(insertSql, parameters);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ZoomAdminAuditService:LogAsync] Exception: {ex.Message}");
            }
        }

        private async Task EnsureTableAsync()
        {
            const string ensureTableSql = @"
IF OBJECT_ID('dbo.ZoomAdminOperationLogs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ZoomAdminOperationLogs
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        AdminEmail NVARCHAR(320) NULL,
        ActionType NVARCHAR(64) NOT NULL,
        TargetEmails NVARCHAR(2000) NULL,
        [Timestamp] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        Result NVARCHAR(32) NOT NULL,
        Message NVARCHAR(2000) NULL
    );
END";

            await _context.Database.ExecuteSqlRawAsync(ensureTableSql);
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}

