using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Toplanti.Business.HttpClients;
using Toplanti.Core.Utilities.Helper;

namespace Toplanti.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SsoController : ControllerBase
    {
        private readonly ISsoApi _ssoApi;

        public SsoController(ISsoApi ssoApi)
        {
            _ssoApi = ssoApi;
        }


        [HttpGet("getcenterperson")]
        public ActionResult GetCenterPerson()
        {
            var userId = new UserCookie().UserId();
            var result = _ssoApi.Person(userId);
            return Ok(result);
        }
    }
}
