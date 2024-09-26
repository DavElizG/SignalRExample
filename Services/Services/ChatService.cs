using DataAcces.Entities;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataAcces.Data;

namespace Services.Services
{
    public class ChatService : IChatService
    {
        private readonly ChatMessagesContext _context;

        public ChatService(ChatMessagesContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ChatMessage>> GetMessagesAsync()
        {
            return await _context.ChatMessages.ToListAsync();
        }

        public async Task AddMessageAsync(ChatMessage message)
        {
            _context.ChatMessages.Add(message);
            await _context.SaveChangesAsync();
        }
    }
}
