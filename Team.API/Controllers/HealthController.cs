using Microsoft.AspNetCore.Mvc;

namespace Team.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        [Route("/health")]
        public IActionResult Health()
        {
            return Ok(new
            {
                status = "Healthy",
                timestamp = DateTime.UtcNow,
                version = "1.0.0",
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"
            });
        }

        [HttpGet]
        [Route("/")]
        public IActionResult Root()
        {
            return Ok(new
            {
                message = "Team API is running",
                status = "OK",
                timestamp = DateTime.UtcNow,
                endpoints = new
                {
                    health = "/health",
                    swagger = "/swagger",
                    api = "/api"
                }
            });
        }
    }
}