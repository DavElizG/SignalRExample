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
                Recipient = recipient,
                Message = message,
                Timestamp = DateTime.UtcNow
            };

            await _chatService.AddMessageAsync(chatMessage);
            // Usamos Clients.All y enviamos todos los datos para que el cliente pueda filtrar
            await Clients.All.SendAsync("ReceiveMessage", user, recipient, message, chatMessage.Timestamp);
        }

        // Nuevo método para enviar mensajes a todos los usuarios
        public async Task SendBroadcastMessage(string user, string message)
        {
            var chatMessage = new ChatMessage
            {
                User = user,
                Recipient = "BROADCAST", // Marcador especial para mensajes globales
                Message = message,
                Timestamp = DateTime.UtcNow
            };

            // Guardar el mensaje en la base de datos
            await _chatService.AddMessageAsync(chatMessage);
            
            // Enviar el mensaje a todos los clientes conectados
            await Clients.All.SendAsync("ReceiveBroadcastMessage", user, message, chatMessage.Timestamp);
        }

        // Método para notificar cuando un usuario se conecta
        public async Task AnnounceJoin(string username)
        {
            // Almacenar temporalmente el nombre de usuario
            Context.Items["Username"] = username;
            
            // Notificar a todos que un nuevo usuario se ha unido
            await Clients.Others.SendAsync("UserJoined", username, DateTime.UtcNow);
        }

        // Override del método OnConnectedAsync para registrar las conexiones
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            
            // Registrar la conexión para diagnóstico
            var connectionId = Context.ConnectionId;
            Console.WriteLine($"Nueva conexión: {connectionId}");
        }

        // Override del método OnDisconnectedAsync para limpiar
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            // Obtener el nombre de usuario si existe
            if (Context.Items.TryGetValue("Username", out var username))
            {
                // Notificar a otros usuarios que este usuario se ha desconectado
                await Clients.Others.SendAsync("UserLeft", username, DateTime.UtcNow);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
