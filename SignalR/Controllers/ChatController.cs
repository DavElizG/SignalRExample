using DataAcces.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Services.Hubs;
using Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SignalR.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly ILogger<ChatController> _logger;

        public ChatController(IChatService chatService, IHubContext<ChatHub> hubContext, ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _hubContext = hubContext;
            _logger = logger;
        }

        // Método simplificado para diagnosticar el problema
        [HttpGet("{user}/{recipient}")]
        public async Task<IActionResult> GetMessages(string user, string recipient)
        {
            try
            {
                _logger.LogInformation($"Inicio: Obteniendo mensajes entre {user} y {recipient}");
                
                // Validar parámetros de entrada
                if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(recipient))
                {
                    _logger.LogWarning("Parámetros inválidos: user o recipient están vacíos");
                    return BadRequest(new { success = false, message = "Los parámetros user y recipient son obligatorios" });
                }

                // Usar un try-catch específico para las operaciones de base de datos
                IEnumerable<ChatMessage> messages;
                try
                {
                    // Simplificar la llamada al servicio para minimizar puntos de falla
                    messages = await _chatService.GetMessagesBetweenUsersAsync(user, recipient);
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, $"Error de base de datos: {dbEx.Message}");
                    return StatusCode(500, new { 
                        success = false, 
                        message = "Error al recuperar mensajes de la base de datos", 
                        errorDetails = dbEx.Message 
                    });
                }
                
                // Si llegamos aquí, la operación fue exitosa
                _logger.LogInformation($"Éxito: Obteniendo mensajes entre {user} y {recipient}");
                return Ok(new { success = true, data = messages });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error general al obtener mensajes entre {user} y {recipient}: {ex.Message}");
                return StatusCode(500, new { 
                    success = false, 
                    message = "Error al procesar la solicitud", 
                    errorDetails = ex.Message 
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] ChatMessage message)
        {
            try
            {
                if (message == null)
                {
                    _logger.LogWarning("Mensaje nulo recibido");
                    return BadRequest(new { success = false, message = "El mensaje no puede ser nulo" });
                }
                
                if (string.IsNullOrEmpty(message.User) || string.IsNullOrEmpty(message.Recipient) || string.IsNullOrEmpty(message.Message))
                {
                    _logger.LogWarning("Mensaje inválido recibido: falta información requerida");
                    return BadRequest(new { success = false, message = "Campos requeridos: User, Recipient y Message" });
                }

                _logger.LogInformation($"Enviando mensaje de {message.User} a {message.Recipient}");
                
                message.Id = 0; // Asegúrate de que el Id sea 0 para que se genere automáticamente
                message.Timestamp = DateTime.UtcNow;
                
                // Crear un token de cancelación con timeout de 10 segundos
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                
                // Guardar el mensaje en la base de datos con timeout
                await Task.Run(async () => await _chatService.AddMessageAsync(message), cts.Token);
                
                // Notificar a través de SignalR, si falla, loggear pero no interrumpir
                try
                {
                    await _hubContext.Clients.All.SendAsync("ReceiveMessage", 
                        message.User, 
                        message.Recipient, 
                        message.Message,
                        cts.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al notificar por SignalR, pero el mensaje fue guardado");
                }
                
                return Ok(new { success = true, data = message });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning($"Timeout al enviar mensaje de {message?.User} a {message?.Recipient}");
                return StatusCode(504, new { success = false, message = "La operación ha tardado demasiado tiempo en completarse" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al enviar mensaje de {message?.User} a {message?.Recipient}");
                return StatusCode(500, new { success = false, message = "Error al enviar mensaje", error = ex.Message });
            }
        }
    }
}
