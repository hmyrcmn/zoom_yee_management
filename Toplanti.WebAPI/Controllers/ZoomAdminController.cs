using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Toplanti.Business.Abstract;
using Toplanti.Entities.DTOs;
using Toplanti.WebAPI.Services;

namespace Toplanti.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ZoomAdminController : ControllerBase
    {
        private readonly IZoomService _zoomService;
        private readonly IZoomAdminAuditService _zoomAdminAuditService;

        public ZoomAdminController(IZoomService zoomService, IZoomAdminAuditService zoomAdminAuditService)
        {
            _zoomService = zoomService;
            _zoomAdminAuditService = zoomAdminAuditService;
        }

        [HttpGet("users")]
        public async Task<ActionResult> GetUsers()
        {
            try
            {
                var result = await _zoomService.GetWorkspaceUsers();
                if (!result.Success)
                {
                    if (IsAuthorizationFailure(result.Message))
                    {
                        return StatusCode(403, new { success = false, message = result.Message });
                    }

                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { success = false, message = ex.Message });
            }
            catch (System.Exception ex)
            {
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
                    email = request.email ?? string.Empty,
                    first_name = request.first_name ?? request.firstName ?? string.Empty,
                    last_name = request.last_name ?? request.lastName ?? string.Empty,
                    password = string.Empty,
                    type = request.type ?? 1
                };

                var result = await _zoomService.AddUserToZoom(mappedRequest);
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

                await _zoomAdminAuditService.LogAsync(
                    GetRequesterEmail(),
                    "ADD_USER",
                    mappedRequest.email,
                    "Success",
                    result.Message);

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { success = false, message = ex.Message });
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[ZoomAdminController:AddUserToZoom] Exception: {ex.Message}");
                return BadRequest(new { success = false, message = $"Zoom user eklenirken hata: {ex.Message}" });
            }
        }

        [HttpDelete("delete-user")]
        public async Task<ActionResult> DeleteUserFromZoom([FromQuery] string email)
        {
            try
            {
                var requesterEmail = GetRequesterEmail();
                if (IsSameEmail(requesterEmail, email))
                {
                    return StatusCode(403, new { success = false, message = "Kendi hesabınızı silemezsiniz!" });
                }

                var result = await _zoomService.DeleteUserFromZoom(email);
                if (!result.Success)
                {
                    if (IsAuthorizationFailure(result.Message))
                    {
                        return StatusCode(403, new { success = false, message = result.Message });
                    }

                    return BadRequest(result);
                }

                await _zoomAdminAuditService.LogAsync(
                    requesterEmail,
                    "DELETE_USER",
                    email,
                    "Success",
                    result.Message);

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { success = false, message = ex.Message });
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[ZoomAdminController:DeleteUserFromZoom] Exception: {ex.Message}");
                return BadRequest(new { success = false, message = $"Zoom user silinirken hata: {ex.Message}" });
            }
        }

        [HttpPost("bulk-delete")]
        public async Task<ActionResult> BulkDeleteUsersFromZoom([FromBody] ZoomBulkDeleteRequest request)
        {
            try
            {
                var requesterEmail = GetRequesterEmail();
                var targetEmails = (request?.Emails ?? new List<string>())
                    .Where(email => !string.IsNullOrWhiteSpace(email))
                    .Select(email => email.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (targetEmails.Any(email => IsSameEmail(requesterEmail, email)))
                {
                    return StatusCode(403, new { success = false, message = "Kendi hesabınızı silemezsiniz!" });
                }

                var result = await _zoomService.DeleteUsersFromZoom(targetEmails);
                if (!result.Success)
                {
                    if (IsAuthorizationFailure(result.Message))
                    {
                        return StatusCode(403, new { success = false, message = result.Message });
                    }

                    return BadRequest(result);
                }

                await _zoomAdminAuditService.LogAsync(
                    requesterEmail,
                    "BULK_DELETE",
                    string.Join(",", targetEmails),
                    "Success",
                    result.Message);

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { success = false, message = ex.Message });
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[ZoomAdminController:BulkDeleteUsersFromZoom] Exception: {ex.Message}");
                return BadRequest(new { success = false, message = $"Zoom bulk delete sÄ±rasÄ±nda hata: {ex.Message}" });
            }
        }

        private static bool IsAuthorizationFailure(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return message.Contains("Yetkiniz yok", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Authorization denied", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase);
        }

        private string GetRequesterEmail()
        {
            var email = User?.FindFirst(ClaimTypes.Email)?.Value;
            if (!string.IsNullOrWhiteSpace(email))
            {
                return email.Trim();
            }

            email = User?.FindFirst("email")?.Value;
            return string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim();
        }

        private static bool IsSameEmail(string? left, string? right)
        {
            return string.Equals(
                (left ?? string.Empty).Trim(),
                (right ?? string.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
