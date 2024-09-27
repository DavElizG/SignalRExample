using DataAcces.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Services.Hubs;
using Services.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

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

        [HttpGet("{user}/{recipient}")]
        public async Task<IEnumerable<ChatMessage>> GetMessages(string user, string recipient)
        {
            return await _chatService.GetMessagesBetweenUsersAsync(user, recipient);
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] ChatMessage message)
        {
            if (message == null || string.IsNullOrEmpty(message.User) || string.IsNullOrEmpty(message.Recipient) || string.IsNullOrEmpty(message.Message))
            {
                return BadRequest("Invalid message.");
            }

            message.Id = 0; // Asegúrate de que el Id sea 0 para que se genere automáticamente
            message.Timestamp = DateTime.UtcNow;
            await _chatService.AddMessageAsync(message);
            await _hubContext.Clients.User(message.Recipient).SendAsync("ReceiveMessage", message.User, message.Message);
            await _hubContext.Clients.User(message.User).SendAsync("ReceiveMessage", message.User, message.Message); // Para que el remitente también reciba el mensaje

            return Ok(message);
        }
    }
}
