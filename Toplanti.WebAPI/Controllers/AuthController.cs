using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using Toplanti.Business.Abstract;
using Toplanti.DataAccess.Concrete.EntityFramework.Contexts;
using Toplanti.Entities.DTOs;

namespace Toplanti.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IUserService _userService;
        private readonly ToplantiContext _context;

        public AuthController(IAuthService authService, IUserService userService, ToplantiContext context)
        {
            _authService = authService;
            _userService = userService;
            _context = context;
        }

        [HttpGet("cookie")]
        public ActionResult Cookie()
        {
            var selectCookie = Request.Cookies[".AspNet.SharedCookie"];

            bool select = false;

            if (selectCookie != null)
            {
                bool.TryParse(selectCookie, out select);
                select = true;
            }
            return Ok(select);
        }

        [HttpGet("rol")]
        public ActionResult Rol()
        {
            return Ok(_userService.RoleName());
        }

        [HttpGet("logout")]
        public async Task Logout()
        {
            var authProperties = new AuthenticationProperties() { IsPersistent = true };
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme, authProperties);
        }

        [HttpPost("login")]
        public ActionResult Login(UserForLoginDto userForLoginDto)
        {
            var userToLogin = _authService.Login(userForLoginDto);
            if (!userToLogin.Success)
            {
                LogZoomAction("LOGIN", userForLoginDto?.Email, false, userToLogin.Message);
                if (!string.IsNullOrWhiteSpace(userToLogin.Message)
                    && userToLogin.Message.StartsWith("Zoom kayd", StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, userToLogin);
                }
                return BadRequest(userToLogin);
            }

            userToLogin.Data.Department ??= string.Empty;
            LogZoomAction("LOGIN", userForLoginDto?.Email, true, userToLogin.Message);
            return Ok(userToLogin);
        }

        [HttpGet("userinfo")]
        public ActionResult UserInfo()
        {
            var result = _authService.UserInfo();
            if (result.Data == null)
            {
                return Unauthorized(result);
            }
            result.Data.Department ??= string.Empty;
            return Ok(result);
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

                const string insertSql = @"
INSERT INTO dbo.ZoomActionLogs (UserId, Action, TargetEmail, Success, Message, CreatedAt)
VALUES (@UserId, @Action, @TargetEmail, @Success, @Message, SYSUTCDATETIME())";

                var parameters = new[]
                {
                    new SqlParameter("@UserId", DBNull.Value),
                    new SqlParameter("@Action", action ?? string.Empty),
                    new SqlParameter("@TargetEmail", (object?)targetEmail ?? DBNull.Value),
                    new SqlParameter("@Success", success),
                    new SqlParameter("@Message", (object?)message ?? DBNull.Value),
                };

                _context.Database.ExecuteSqlRaw(insertSql, parameters);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthController:LogZoomAction] Exception: {ex.Message}");
            }
        }
    }
}
