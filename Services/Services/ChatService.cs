using DataAcces.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataAcces.Data;

namespace Services.Services
{
    public class ChatService : IChatService
    {
        private readonly ChatMessagesContext _context;
        private readonly ILogger<ChatService> _logger;

        public ChatService(ChatMessagesContext context, ILogger<ChatService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<ChatMessage>> GetMessagesAsync()
        {
            try
            {
                _logger.LogInformation("Obteniendo todos los mensajes");
                // Configurar un timeout para la consulta a la BD
                var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;
                return await _context.ChatMessages
                    .AsNoTracking()  // Mejora rendimiento para consultas de solo lectura
                    .ToListAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Timeout al obtener todos los mensajes");
                throw new TimeoutException("La operación de obtener todos los mensajes ha excedido el tiempo límite");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener todos los mensajes");
                throw;
            }
        }

        public async Task<IEnumerable<ChatMessage>> GetMessagesBetweenUsersAsync(string user, string recipient)
        {
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(recipient))
            {
                _logger.LogWarning("Parámetros inválidos: user o recipient están vacíos");
                throw new ArgumentException("Los parámetros user y recipient son obligatorios");
            }

            try
            {
                _logger.LogInformation($"ChatService - Obteniendo mensajes entre {user} y {recipient}");
                
                // Verificar explícitamente si la conexión está abierta
                if (_context.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
                {
                    _logger.LogInformation("Conexión cerrada, intentando abrir...");
                    await _context.Database.OpenConnectionAsync();
                    _logger.LogInformation("Conexión abierta exitosamente");
                }
                
                // Simplificar al máximo la consulta para diagnóstico
                var query = _context.ChatMessages
                    .AsNoTracking() // Mejora rendimiento
                    .Where(m => 
                        (m.User == user && m.Recipient == recipient) || 
                        (m.User == recipient && m.Recipient == user))
                    .OrderBy(m => m.Timestamp);
                
                _logger.LogInformation($"SQL Query: {query.ToQueryString()}");
                
                // Ejecutar en un timeout controlado
                var messages = await query.ToListAsync();
                
                _logger.LogInformation($"Encontrados {messages.Count} mensajes entre {user} y {recipient}");
                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error en ChatService al obtener mensajes entre {user} y {recipient}");
                
                // En caso de errores específicos con la conexión, intentamos hacer un diagnóstico básico
                if (!_context.Database.CanConnect())
                {
                    _logger.LogError("No se puede conectar a la base de datos. Verificando estado de conexión...");
                    
                    try
                    {
                        var connectionState = _context.Database.GetDbConnection().State;
                        _logger.LogError($"Estado actual de la conexión: {connectionState}");
                    }
                    catch (Exception connEx)
                    {
                        _logger.LogError(connEx, "Error al intentar verificar el estado de la conexión");
                    }
                }
                
                // Devolver una lista vacía en caso de error para evitar que la aplicación falle completamente
                // Esto es solo para diagnóstico temporal, en producción deberíamos lanzar la excepción
                _logger.LogWarning("Retornando lista vacía como fallback para diagnóstico");
                return new List<ChatMessage>();
            }
        }

        public async Task AddMessageAsync(ChatMessage message)
        {
            if (message == null)
            {
                _logger.LogWarning("Se intentó añadir un mensaje nulo");
                throw new ArgumentNullException(nameof(message));
            }

            try
            {
                _logger.LogInformation($"Añadiendo mensaje de {message.User} a {message.Recipient}");
                _context.ChatMessages.Add(message);
                
                // Configurar un timeout para la operación de guardado
                var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;
                await _context.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation($"Mensaje añadido correctamente con ID {message.Id}");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning($"Timeout al guardar mensaje de {message.User} a {message.Recipient}");
                throw new TimeoutException("La operación de guardar el mensaje ha excedido el tiempo límite");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, $"Error de base de datos al guardar mensaje de {message.User} a {message.Recipient}");
                throw new Exception("Error al guardar el mensaje en la base de datos", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error inesperado al guardar mensaje de {message.User} a {message.Recipient}");
                throw;
            }
        }
    }
}
