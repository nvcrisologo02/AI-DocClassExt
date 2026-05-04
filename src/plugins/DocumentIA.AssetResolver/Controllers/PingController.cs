using Microsoft.AspNetCore.Mvc;

namespace DocumentIA.AssetResolver.Controllers;

[ApiController]
public class PingController : ControllerBase
{
    [HttpGet("api/assets/ping")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Ping()
    {
        return Ok(new { status = "ok" });
    }
}
