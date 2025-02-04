using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MamAcars.Models
{
    public class BlackBoxBasicInformation
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool onGround { get; set; }
        public int Altitude { get; set; }

        public int AGLAltitude { get; set; }

        public int Heading { get; set; }

        public int GroundSpeedKnots { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public override string ToString()
        {
            return $"Latitude: {Latitude}, Longitude: {Longitude}, onGround: {onGround}, Altitude: {Altitude}, " +
                   $"Heading: {Heading}, GroundSpeedKnots: {GroundSpeedKnots}, Timestamp: {Timestamp:O}";
        }
    }
}
