using Microsoft.AspNetCore.Mvc;

namespace Crypto.Assistant.WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SystemController : ControllerBase
    {
        [HttpGet]
        [HttpHead]
        [Route("status")]
        public IActionResult Status()
        {
            return Ok();
        }
    }
}
