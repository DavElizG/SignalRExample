using DataAcces.Entities;
using Microsoft.AspNetCore.SignalR;
using Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace Services.Hubs
{
    public class ChatHub : Hub
    {
        private readonly IChatService _chatService;

        public ChatHub(IChatService chatService)
        {
            _chatService = chatService;
        }

        public async Task SendMessage(string user, string recipient, string message)
        {
            var chatMessage = new ChatMessage
            {
                User = user,
                Recipient = recipient, // Nuevo campo
                Message = message,
                Timestamp = DateTime.UtcNow
            };

            await _chatService.AddMessageAsync(chatMessage);
            await Clients.User(recipient).SendAsync("ReceiveMessage", user, message);
            await Clients.User(user).SendAsync("ReceiveMessage", user, message); // Para que el remitente también reciba el mensaje
        }
    }
}
