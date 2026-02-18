using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using Toplanti.Business.HttpClients;

namespace Toplanti.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SsoController : ControllerBase
    {
        private readonly ISsoApi _ssoApi;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SsoController(ISsoApi ssoApi, IHttpContextAccessor httpContextAccessor)
        {
            _ssoApi = ssoApi;
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpGet("getcenterperson")]
        public ActionResult GetCenterPerson()
        {
            try
            {
                var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out var userId) || userId <= 0)
                {
                    return Ok(new List<object>());
                }

                var result = _ssoApi.Person(userId);
                if (result == null)
                {
                    return Ok(new List<object>());
                }

                return Ok(result);
            }
            catch (Exception)
            {
                return Ok(new List<object>());
            }
        }
    }
}
