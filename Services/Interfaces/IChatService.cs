using DataAcces.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Interfaces
{
    public interface IChatService
    {
        Task<IEnumerable<ChatMessage>> GetMessagesAsync();
        Task<IEnumerable<ChatMessage>> GetMessagesBetweenUsersAsync(string user, string recipient); // Nuevo método
        Task AddMessageAsync(ChatMessage message);
    }
}
