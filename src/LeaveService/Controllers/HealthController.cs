using Microsoft.AspNetCore.Mvc;

namespace LeaveService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { Status = "Healthy", Service = "LeaveService", Timestamp = DateTime.UtcNow });
    }
}
