using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Toplanti.DataAccess.Concrete.EntityFramework.Contexts;

namespace Toplanti.WebAPI.Controllers
{
    [Route("api/health")]
    [ApiController]
    [AllowAnonymous]
    public sealed class HealthCheckController : ControllerBase
    {
        private const int LdapConnectTimeoutSeconds = 3;
        private readonly ToplantiContext _context;
        private readonly IConfiguration _configuration;

        public HealthCheckController(ToplantiContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<ActionResult> Get(CancellationToken cancellationToken)
        {
            var dbCheck = await CheckDatabaseSeedAsync(cancellationToken);

            var ldapCheck = await CheckLdapConnectivityAsync(cancellationToken);
            var jwtCheck = CheckJwtConfiguration();

            var checks = new
            {
                Database = dbCheck,
                Ldap = ldapCheck,
                Jwt = jwtCheck
            };

            if (dbCheck.Success && ldapCheck.Success && jwtCheck.Success)
            {
                return Ok(new
                {
                    Success = true,
                    Code = "OK",
                    Message = "System healthy.",
                    Checks = checks
                });
            }

            return StatusCode(503, new
            {
                Success = false,
                Code = ResolveFailureCode(dbCheck.Success, ldapCheck.Success, jwtCheck.Success),
                Message = "System unhealthy.",
                Checks = checks
            });
        }

        private async Task<HealthProbeResult> CheckDatabaseSeedAsync(CancellationToken cancellationToken)
        {
            try
            {
                var hasSeedData = await _context.ZoomStatuses
                    .AsNoTracking()
                    .AnyAsync(cancellationToken);

                return hasSeedData
                    ? new HealthProbeResult(true, "OK", "Zoom status catalog is available.")
                    : new HealthProbeResult(false, "DB_SEED_MISSING", "Zoom status catalog is empty.");
            }
            catch (Exception ex)
            {
                return new HealthProbeResult(false, "DB_SEED_MISSING", $"Zoom status catalog unavailable: {ex.Message}");
            }
        }

        private async Task<HealthProbeResult> CheckLdapConnectivityAsync(CancellationToken cancellationToken)
        {
            var host = (_configuration["LdapSettings:Host"] ?? string.Empty).Trim();
            var port = _configuration.GetValue<int?>("LdapSettings:Port") ?? 389;

            if (string.IsNullOrWhiteSpace(host))
            {
                return new HealthProbeResult(false, "LDAP_CONFIG_MISSING", "LdapSettings:Host is not configured.");
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(LdapConnectTimeoutSeconds));

            try
            {
                using var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(host, port, timeoutCts.Token);

                return new HealthProbeResult(true, "OK", $"LDAP reachable at {host}:{port}.");
            }
            catch (OperationCanceledException)
            {
                return new HealthProbeResult(false, "LDAP_CONNECTION_TIMEOUT", $"LDAP connection timed out ({host}:{port}).");
            }
            catch (Exception ex)
            {
                return new HealthProbeResult(false, "LDAP_CONNECTION_FAILED", $"LDAP connection failed: {ex.Message}");
            }
        }

        private HealthProbeResult CheckJwtConfiguration()
        {
            var securityKey = (_configuration["TokenOptions:SecurityKey"] ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(securityKey))
            {
                return new HealthProbeResult(false, "JWT_SECURITY_KEY_MISSING", "TokenOptions:SecurityKey is empty.");
            }

            var keyBytes = Encoding.UTF8.GetBytes(securityKey);
            if (keyBytes.Length < 32)
            {
                return new HealthProbeResult(false, "JWT_SECURITY_KEY_INVALID", "TokenOptions:SecurityKey must be at least 32 bytes.");
            }

            try
            {
                _ = new SymmetricSecurityKey(keyBytes);
                return new HealthProbeResult(true, "OK", "JWT security key loaded.");
            }
            catch (Exception ex)
            {
                return new HealthProbeResult(false, "JWT_SECURITY_KEY_INVALID", $"JWT security key is invalid: {ex.Message}");
            }
        }

        private static string ResolveFailureCode(bool dbHealthy, bool ldapHealthy, bool jwtHealthy)
        {
            if (!dbHealthy)
            {
                return "DB_SEED_MISSING";
            }

            if (!ldapHealthy)
            {
                return "LDAP_CONNECTION_FAILED";
            }

            if (!jwtHealthy)
            {
                return "JWT_SECURITY_KEY_INVALID";
            }

            return "HEALTHCHECK_FAILED";
        }

        private sealed record HealthProbeResult(bool Success, string Code, string Message);
    }
}
