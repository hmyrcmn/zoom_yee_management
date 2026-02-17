using Microsoft.AspNetCore.Mvc;
using Toplanti.Business.Abstract;
using Toplanti.Business.BusinessAspects.Autofac;

namespace Toplanti.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly IAuthTestService _authTestService;

        public TestController(IAuthTestService authTestService)
        {
            _authTestService = authTestService;
        }

        [HttpGet("admin-only")]
        public IActionResult AdminOnlyTest()
        {
            var result = _authTestService.AdminOnlyTest();
            if (result.Success) return Ok(result.Message);
            return BadRequest(result.Message);
        }

        [HttpGet("user-only")]
        [SecuredOperation("User,Admin")]
        public IActionResult UserOnlyTest()
        {
            return Ok("You have User or Admin access.");
        }
    }
}
