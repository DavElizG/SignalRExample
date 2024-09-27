using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAcces.Entities
{
    public class ChatMessage
    {
        public int Id { get; set; }
        public string User { get; set; }
        public string Recipient { get; set; } // Nuevo campo
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
