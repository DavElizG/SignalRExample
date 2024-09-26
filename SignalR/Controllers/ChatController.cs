using DataAcces.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Services.Hubs;
using Services.Interfaces;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace SignalR.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly IHubContext<ChatHub> _hubContext;

        public ChatController(IChatService chatService, IHubContext<ChatHub> hubContext)
        {
            _chatService = chatService;
            _hubContext = hubContext;
        }


        [HttpGet]
        public async Task<IEnumerable<ChatMessage>> GetMessages()
        {
            return await _chatService.GetMessagesAsync();
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] ChatMessage message)
        {
            if (message == null || string.IsNullOrEmpty(message.User) || string.IsNullOrEmpty(message.Message))
            {
                return BadRequest("Invalid message.");
            }

            message.Timestamp = DateTime.UtcNow;
            await _chatService.AddMessageAsync(message);
            await _hubContext.Clients.All.SendAsync("ReceiveMessage", message.User, message.Message);

            return Ok(message);
        }
    }
}
