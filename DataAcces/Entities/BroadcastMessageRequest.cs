using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAcces.Entities
{
    // Clase para el request de mensaje broadcast
    public class BroadcastMessageRequest
    {
        public string User { get; set; }
        public string Message { get; set; }
    }
}