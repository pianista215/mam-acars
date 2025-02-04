using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MamAcars.Models
{
    public class FlightData
    {
        public string FlightId { get; set; }
        public string PilotComments { get; set; }
        public string Aircraft { get; set; }
        public List<FlightEvent> Events { get; set; }
    }
}
