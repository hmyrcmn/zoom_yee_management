using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using Toplanti.Business.Abstract;
using Toplanti.Entities.DTOs;

namespace Toplanti.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ZoomAdminController : ControllerBase
    {
        private readonly IZoomService _zoomService;

        public ZoomAdminController(IZoomService zoomService)
        {
            _zoomService = zoomService;
        }

        [HttpGet("users")]
        public ActionResult GetUsers()
        {
            // Temporary simulated payload for UI table population.
            var users = new List<object>
            {
                new { email = "ali.yilmaz@yee.org.tr", first_name = "Ali", last_name = "Yilmaz", status = "active" },
                new { email = "ayse.kaya@yee.org.tr", first_name = "Ayse", last_name = "Kaya", status = "active" },
                new { email = "mehmet.demir@yee.org.tr", first_name = "Mehmet", last_name = "Demir", status = "inactive" }
            };

            return Ok(users);
        }

        [HttpPost("add-user")]
        public async Task<ActionResult> AddUserToZoom([FromBody] ZoomUserCreatedResponse request)
        {
            var result = await _zoomService.AddUserToZoom(request);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpDelete("delete-user")]
        public async Task<ActionResult> DeleteUserFromZoom([FromQuery] string email)
        {
            var result = await _zoomService.DeleteUserFromZoom(email);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpPost("bulk-delete")]
        public async Task<ActionResult> BulkDeleteUsersFromZoom([FromBody] ZoomBulkDeleteRequest request)
        {
            var result = await _zoomService.DeleteUsersFromZoom(request?.Emails ?? new System.Collections.Generic.List<string>());
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
    }
}
