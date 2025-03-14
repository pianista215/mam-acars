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
        public bool OnGround { get; set; }
        public int Altitude { get; set; }

        public int AGLAltitude { get; set; }

        public int Heading { get; set; }

        public int GroundSpeedKnots { get; set; }
        public int IasKnots { get; set; }

        public int QnhSet { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public bool[] EnginesStarted { get; set; }

        public int FlapsPercentage { get; set; }

        public bool GearUp { get; set; }

        public override string ToString()
        {
            // TODO: UNAI ACTUALIZA
            return $"Latitude: {Latitude}, Longitude: {Longitude}, OnGround: {OnGround}, Altitude: {Altitude}, " +
                   $"Heading: {Heading}, GroundSpeedKnots: {GroundSpeedKnots}, Timestamp: {Timestamp:O}";
        }
    }
}
