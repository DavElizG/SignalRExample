using DataAcces.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Services.Services
{
    public class MessageCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MessageCleanupService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);
        private readonly int _messageLimit = 100;

        public MessageCleanupService(
            IServiceProvider serviceProvider,
            ILogger<MessageCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Servicio de limpieza de mensajes iniciado");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupMessagesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error durante la limpieza de mensajes");
                }

                // Esperar 5 minutos antes de la próxima comprobación
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task CleanupMessagesAsync()
        {
            // Crear un nuevo scope para obtener el contexto de base de datos
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ChatMessagesContext>();
            
            // Contar mensajes en la base de datos
            var messageCount = await dbContext.ChatMessages.CountAsync();
            
            // Si hay más mensajes que el límite, eliminar los más antiguos
            if (messageCount > _messageLimit)
            {
                _logger.LogInformation($"Se encontraron {messageCount} mensajes, superando el límite de {_messageLimit}. Procediendo a eliminar los mensajes más antiguos.");
                
                // Calcular cuántos mensajes debemos eliminar
                var messagesToDelete = messageCount - _messageLimit;
                
                // Obtener IDs de los mensajes más antiguos para eliminar
                var oldestMessageIds = await dbContext.ChatMessages
                    .OrderBy(m => m.Timestamp) // Ordenar por fecha/hora (asumiendo que existe un campo Timestamp)
                    .Take(messagesToDelete)
                    .Select(m => m.Id)
                    .ToListAsync();
                
                // Eliminar los mensajes más antiguos
                if (oldestMessageIds.Any())
                {
                    dbContext.ChatMessages.RemoveRange(
                        dbContext.ChatMessages.Where(m => oldestMessageIds.Contains(m.Id)));
                    
                    var deleted = await dbContext.SaveChangesAsync();
                    _logger.LogInformation($"Se eliminaron {deleted} mensajes antiguos");
                }
            }
            else
            {
                _logger.LogDebug($"Número actual de mensajes ({messageCount}) está por debajo del límite ({_messageLimit}). No es necesario eliminar.");
            }
        }
    }
}