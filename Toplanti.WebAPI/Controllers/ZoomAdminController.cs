using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Toplanti.Business.Abstract;
using Toplanti.DataAccess.Concrete.EntityFramework.Contexts;
using Toplanti.Entities.DTOs;

namespace Toplanti.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ZoomAdminController : ControllerBase
    {
        private readonly IZoomService _zoomService;
        private readonly ToplantiContext _context;

        public ZoomAdminController(IZoomService zoomService, ToplantiContext context)
        {
            _zoomService = zoomService;
            _context = context;
        }

        [HttpGet("users")]
        public async Task<ActionResult> GetUsers()
        {
            try
            {
                var result = await _zoomService.GetWorkspaceUsers();
                LogZoomAction("GET_USERS", null, result.Success, result.Message);
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (System.Exception ex)
            {
                LogZoomAction("GET_USERS", null, false, ex.Message);
                System.Console.WriteLine($"[ZoomAdminController:GetUsers] Exception: {ex.Message}");
                return BadRequest(new { success = false, message = $"Zoom users alÄ±nÄ±rken hata: {ex.Message}" });
            }
        }

        [HttpPost("add-user")]
        public async Task<ActionResult> AddUserToZoom([FromBody] AddUserDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (request == null)
                {
                    return BadRequest(new { success = false, message = "Geçersiz istek gövdesi." });
                }

                var mappedRequest = new ZoomUserCreatedResponse
                {
                    email = request?.email,
                    first_name = request?.first_name ?? request?.firstName,
                    last_name = request?.last_name ?? request?.lastName,
                    password = null,
                    type = request?.type ?? 1
                };

                var result = await _zoomService.AddUserToZoom(mappedRequest);
                LogZoomAction("ADD_USER", mappedRequest?.email, result.Success, result.Message);
                if (!result.Success)
                {
                    var msg = result.Message ?? string.Empty;
                    if (msg.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                        || msg.Contains("zaten hesapta mevcut", StringComparison.OrdinalIgnoreCase))
                    {
                        return StatusCode(409, new { success = false, message = result.Message });
                    }

                    return BadRequest(new { success = false, message = result.Message });
                }

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogZoomAction("ADD_USER", request?.email, false, ex.Message);
                return StatusCode(403, new { success = false, message = ex.Message });
            }
            catch (System.Exception ex)
            {
                LogZoomAction("ADD_USER", request?.email, false, ex.Message);
                System.Console.WriteLine($"[ZoomAdminController:AddUserToZoom] Exception: {ex.Message}");
                return BadRequest(new { success = false, message = $"Zoom user eklenirken hata: {ex.Message}" });
            }
        }

        [HttpGet("test-invite")]
        public async Task<ActionResult> TestInvite()
        {
            try
            {
                var testRequest = new ZoomUserCreatedResponse
                {
                    email = "zoom.test.invite@yee.org.tr",
                    first_name = "Zoom",
                    last_name = "Test",
                    password = null,
                    type = 1
                };

                var result = await _zoomService.AddUserToZoom(testRequest);
                LogZoomAction("TEST_INVITE", testRequest.email, result.Success, result.Message);

                if (!result.Success)
                {
                    return BadRequest(new { success = false, message = result.Message, email = testRequest.email });
                }

                return Ok(new { success = true, message = "Test invite gÃ¶nderildi.", email = testRequest.email });
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[ZoomAdminController:TestInvite] Exception: {ex.Message}");
                return BadRequest(new { success = false, message = $"Test invite sÄ±rasÄ±nda hata: {ex.Message}" });
            }
        }

        [HttpDelete("delete-user")]
        public async Task<ActionResult> DeleteUserFromZoom([FromQuery] string email)
        {
            try
            {
                var result = await _zoomService.DeleteUserFromZoom(email);
                LogZoomAction("DELETE_USER", email, result.Success, result.Message);
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (System.Exception ex)
            {
                LogZoomAction("DELETE_USER", email, false, ex.Message);
                System.Console.WriteLine($"[ZoomAdminController:DeleteUserFromZoom] Exception: {ex.Message}");
                return BadRequest(new { success = false, message = $"Zoom user silinirken hata: {ex.Message}" });
            }
        }

        [HttpPost("bulk-delete")]
        public async Task<ActionResult> BulkDeleteUsersFromZoom([FromBody] ZoomBulkDeleteRequest request)
        {
            try
            {
                var result = await _zoomService.DeleteUsersFromZoom(request?.Emails ?? new System.Collections.Generic.List<string>());
                LogZoomAction("BULK_DELETE", string.Join(",", request?.Emails ?? new List<string>()), result.Success, result.Message);
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (System.Exception ex)
            {
                LogZoomAction("BULK_DELETE", string.Join(",", request?.Emails ?? new List<string>()), false, ex.Message);
                System.Console.WriteLine($"[ZoomAdminController:BulkDeleteUsersFromZoom] Exception: {ex.Message}");
                return BadRequest(new { success = false, message = $"Zoom bulk delete sÄ±rasÄ±nda hata: {ex.Message}" });
            }
        }

        private void EnsureZoomAuditTableAndEmailUniqueIndex()
        {
            const string ensureAuditTable = @"
IF OBJECT_ID('dbo.ZoomActionLogs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ZoomActionLogs (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NULL,
        Action NVARCHAR(100) NOT NULL,
        TargetEmail NVARCHAR(320) NULL,
        Success BIT NOT NULL,
        Message NVARCHAR(2000) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END";
            _context.Database.ExecuteSqlRaw(ensureAuditTable);

            const string ensureUserEmailUniqueIndex = @"
SET QUOTED_IDENTIFIER ON;

IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Users'
      AND COLUMN_NAME = 'Email'
      AND DATA_TYPE = 'nvarchar'
      AND CHARACTER_MAXIMUM_LENGTH = -1
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email IS NOT NULL AND LEN(Email) > 320)
    BEGIN
        ALTER TABLE dbo.Users ALTER COLUMN Email NVARCHAR(320) NULL;
    END
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.Users')
      AND name = 'UX_Users_Email'
)
BEGIN
    IF NOT EXISTS (
        SELECT Email
        FROM dbo.Users
        WHERE Email IS NOT NULL
        GROUP BY Email
        HAVING COUNT(*) > 1
    )
    BEGIN
        CREATE UNIQUE INDEX UX_Users_Email ON dbo.Users(Email) WHERE Email IS NOT NULL;
    END
END";
            _context.Database.ExecuteSqlRaw(ensureUserEmailUniqueIndex);
        }

        private void LogZoomAction(string action, string targetEmail, bool success, string message)
        {
            try
            {
                EnsureZoomAuditTableAndEmailUniqueIndex();

                int? userId = null;
                var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdClaim, out var parsed))
                {
                    userId = parsed;
                }

                const string insertSql = @"
INSERT INTO dbo.ZoomActionLogs (UserId, Action, TargetEmail, Success, Message, CreatedAt)
VALUES (@UserId, @Action, @TargetEmail, @Success, @Message, SYSUTCDATETIME())";

                var parameters = new[]
                {
                    new SqlParameter("@UserId", (object?)userId ?? DBNull.Value),
                    new SqlParameter("@Action", action ?? string.Empty),
                    new SqlParameter("@TargetEmail", (object?)targetEmail ?? DBNull.Value),
                    new SqlParameter("@Success", success),
                    new SqlParameter("@Message", (object?)message ?? DBNull.Value),
                };
                _context.Database.ExecuteSqlRaw(insertSql, parameters);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ZoomAdminController:LogZoomAction] Exception: {ex.Message}");
            }
        }
    }
}
