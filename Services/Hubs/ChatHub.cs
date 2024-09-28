using DataAcces.Entities;
using Microsoft.AspNetCore.SignalR;
using Services.Interfaces;

namespace Services.Hubs
{
    public class ChatHub : Hub // El Hub actúa como punto central de la comunicación en tiempo real
    {
        private readonly IChatService _chatService;

        public ChatHub(IChatService chatService)
        {
            _chatService = chatService;
        }

        public async Task SendMessage(string user, string message)
        {
            var chatMessage = new ChatMessage
            {
                User = user,
                Message = message,
                Timestamp = DateTime.UtcNow
            };

            await _chatService.AddMessageAsync(chatMessage); // Guarda el mensaje antes de enviarlo

            // Clients.All envía el mensaje a todos los clientes conectados al hub en tiempo real
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }
    }
}
