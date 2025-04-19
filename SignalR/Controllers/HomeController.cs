using Microsoft.AspNetCore.Mvc;

namespace SignalR.Controllers
{
    [ApiController]
    [Route("/")]
    public class HomeController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new
            {
                status = "running",
                message = "SignalR Chat API is running",
                endpoints = new
                {
                    chat = "/Chat",
                    chatHub = "/chathub",
                    getMessages = "/Chat/{user}/{recipient}"
                }
            });
        }
    }
}