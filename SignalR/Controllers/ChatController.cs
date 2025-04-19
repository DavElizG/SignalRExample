using DataAcces.Entities;
using DataAcces.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Services.Hubs;
using Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly ChatMessagesContext _context;

        public ChatController(IChatService chatService, IHubContext<ChatHub> hubContext, ILogger<ChatController> logger, ChatMessagesContext context)
        {
            _chatService = chatService;
            _hubContext = hubContext;
            _logger = logger;
            _context = context;
        }

        // Método normalizado para ser insensible a mayúsculas/minúsculas
        [HttpGet("{user}/{recipient}")]
        public async Task<IActionResult> GetMessages(string user, string recipient)
        {
            try
            {
                _logger.LogInformation($"Inicio: Obteniendo mensajes entre '{user}' y '{recipient}'");
                
                // Validar parámetros de entrada
                if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(recipient))
                {
                    _logger.LogWarning("Parámetros inválidos: user o recipient están vacíos");
                    return BadRequest(new { success = false, message = "Los parámetros user y recipient son obligatorios" });
                }

                // Normalizar los parámetros para que sean insensibles a mayúsculas/minúsculas
                // Convertir primera letra a mayúscula y el resto a minúscula para mantener consistencia
                user = NormalizeString(user);
                recipient = NormalizeString(recipient);
                
                _logger.LogInformation($"Parámetros normalizados: '{user}' y '{recipient}'");

                // Acceder directamente para evitar problemas con el servicio existente
                var messages = await _context.ChatMessages
                    .AsNoTracking()
                    .Where(m => 
                        (EF.Functions.Collate(m.User, "utf8mb4_general_ci") == EF.Functions.Collate(user, "utf8mb4_general_ci") && 
                         EF.Functions.Collate(m.Recipient, "utf8mb4_general_ci") == EF.Functions.Collate(recipient, "utf8mb4_general_ci")) || 
                        (EF.Functions.Collate(m.User, "utf8mb4_general_ci") == EF.Functions.Collate(recipient, "utf8mb4_general_ci") && 
                         EF.Functions.Collate(m.Recipient, "utf8mb4_general_ci") == EF.Functions.Collate(user, "utf8mb4_general_ci")))
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync();
                
                // Si llegamos aquí, la operación fue exitosa
                _logger.LogInformation($"Éxito: Encontrados {messages.Count()} mensajes entre '{user}' y '{recipient}'");
                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener mensajes entre '{user}' y '{recipient}': {ex.Message}");
                return StatusCode(500, new { 
                    success = false, 
                    message = "Error al procesar la solicitud", 
                    errorDetails = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        // Método auxiliar para normalizar cadenas
        private string NormalizeString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
                
            // Convertir primera letra a mayúscula y el resto a minúscula
            return char.ToUpper(input[0]) + input.Substring(1).ToLower();
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

        [HttpGet("simple/{user}/{recipient}")]
        public async Task<IActionResult> GetMessagesSimple(string user, string recipient)
        {
            try
            {
                _logger.LogInformation($"Obteniendo mensajes entre '{user}' y '{recipient}' (modo simple)");
                
                if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(recipient))
                {
                    return BadRequest(new { success = false, message = "Los parámetros user y recipient son obligatorios" });
                }

                // Normalizar los parámetros
                user = NormalizeString(user);
                recipient = NormalizeString(recipient);
                
                _logger.LogInformation($"Parámetros normalizados: '{user}' y '{recipient}'");

                // Búsqueda insensible a mayúsculas/minúsculas
                var messages = await _context.ChatMessages
                    .AsNoTracking()
                    .Where(m => 
                        (m.User.ToLower() == user.ToLower() && m.Recipient.ToLower() == recipient.ToLower()) || 
                        (m.User.ToLower() == recipient.ToLower() && m.Recipient.ToLower() == user.ToLower()))
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync();
                
                _logger.LogInformation($"Encontrados {messages.Count} mensajes (modo simple)");
                return Ok(new { success = true, data = messages });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener mensajes simple: {ex.Message}");
                return StatusCode(500, new { success = false, error = ex.Message, type = ex.GetType().Name });
            }
        }
        
        // Nuevo endpoint que acepta cualquier formato de mayúsculas/minúsculas
        [HttpGet("flexible/{user}/{recipient}")]
        public async Task<IActionResult> GetMessagesFlexible(string user, string recipient)
        {
            try
            {
                _logger.LogInformation($"Inicio: Obteniendo mensajes con flexibilidad de mayúsculas entre '{user}' y '{recipient}'");
                
                if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(recipient))
                {
                    return BadRequest(new { success = false, message = "Los parámetros user y recipient son obligatorios" });
                }
                
                // Realizar una consulta completamente independiente del servicio
                var messages = new List<ChatMessage>();
                
                try
                {
                    // Intentar diferentes combinaciones de mayúsculas/minúsculas
                    var userLower = user.ToLower();
                    var recipientLower = recipient.ToLower();
                    var userUpper = char.ToUpper(user[0]) + user.Substring(1).ToLower();
                    var recipientUpper = char.ToUpper(recipient[0]) + recipient.Substring(1).ToLower();
                    
                    _logger.LogInformation($"Probando con diferentes combinaciones: [{userLower}/{recipientLower}] y [{userUpper}/{recipientUpper}]");
                    
                    messages = await _context.ChatMessages
                        .FromSqlRaw(@"SELECT * FROM ChatMessages 
                                    WHERE (LOWER(User) = {0} AND LOWER(Recipient) = {1})
                                    OR (LOWER(User) = {1} AND LOWER(Recipient) = {0})
                                    ORDER BY Timestamp",
                                    userLower, recipientLower)
                        .AsNoTracking()
                        .ToListAsync();
                    
                    _logger.LogInformation($"Encontrados {messages.Count} mensajes con SQL directo");
                }
                catch (Exception sqlEx)
                {
                    _logger.LogWarning(sqlEx, "Error con SQL directo, intentando método alternativo");
                    
                    // Plan B: traer todos los mensajes y filtrar en memoria (solo viable si no hay muchos mensajes)
                    var allMessages = await _context.ChatMessages
                        .AsNoTracking()
                        .ToListAsync();
                    
                    messages = allMessages
                        .Where(m => 
                            (m.User.Equals(user, StringComparison.OrdinalIgnoreCase) && 
                             m.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase)) ||
                            (m.User.Equals(recipient, StringComparison.OrdinalIgnoreCase) && 
                             m.Recipient.Equals(user, StringComparison.OrdinalIgnoreCase)))
                        .OrderBy(m => m.Timestamp)
                        .ToList();
                    
                    _logger.LogInformation($"Encontrados {messages.Count} mensajes con método alternativo de {allMessages.Count} totales");
                }
                
                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener mensajes flexible: {ex.Message}");
                return StatusCode(500, new { 
                    success = false, 
                    message = "Error al procesar la solicitud flexible", 
                    errorDetails = ex.Message 
                });
            }
        }
    }
}
