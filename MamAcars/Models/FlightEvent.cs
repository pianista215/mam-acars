using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MamAcars.Models
{
    public class FlightEvent
    {
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Changes { get; set; }
    }
}
