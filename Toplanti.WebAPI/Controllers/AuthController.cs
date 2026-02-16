using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading.Tasks;
using Toplanti.Business.Abstract;
using Toplanti.Entities.DTOs;

namespace Toplanti.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IUserService _userService;

        public AuthController(IAuthService authService, IUserService userService)
        {
            _authService = authService;
            _userService = userService;
        }

        [HttpGet("cookie")]
        public ActionResult Cookie()
        {
            var selectCookie = Request.Cookies[".AspNet.SharedCookie"];
            //var selectCookie2 = HttpContext.Response.Cookies[".AspNet.SharedCookie"];

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
                return BadRequest(userToLogin);
            }
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
            return Ok(result);
        }
    }
}
