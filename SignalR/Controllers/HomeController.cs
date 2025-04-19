using Microsoft.AspNetCore.Mvc;
using System;

namespace SignalR.Controllers
{
    [ApiController]
    [Route("")]
    public class HomeController : ControllerBase
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Get()
        {
            try
            {
                _logger.LogInformation("Endpoint raíz accedido");
                return Ok(new
                {
                    status = "running",
                    message = "SignalR Chat API is running",
                    timestamp = DateTime.UtcNow,
                    endpoints = new
                    {
                        chat = "/Chat",
                        chatHub = "/chathub",
                        getMessages = "/Chat/{user}/{recipient}"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el endpoint raíz");
                return StatusCode(500, new { error = "Error interno del servidor", message = ex.Message });
            }
        }

        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            try
            {
                _logger.LogInformation("Health check solicitado");
                return Ok(new
                {
                    status = "healthy",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en health check");
                return StatusCode(500, new { status = "unhealthy", error = ex.Message });
            }
        }
    }
}