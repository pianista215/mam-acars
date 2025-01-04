using FSUIPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MamAcars.Services
{
    public class FsuipcService
    {

        private Timer _timer;
        private bool _simConnected = false;
        private bool _planeOnAirport = false;

        public bool SimConnected => _simConnected;

        public bool PlaneOnAirport => _planeOnAirport;

        public event Action<bool> SimStatusChanged;

        public event Action<bool> AircraftLocationChanged;

        private Offset<FsLongitude> longitudeOffset = new Offset<FsLongitude>("Basic", 0x0568, 8);
        private Offset<FsLatitude> latitudeOffset = new Offset<FsLatitude>("Basic", 0x0560, 8);

        private FsLatitude latitude;
        private FsLongitude longitude;

        public void startMonitoring(double airportLatitude, double airportLongitude)
        {
            var state = new ExpectedLocation
            {
                AirportLatitude = airportLatitude,
                AirportLongitude = airportLongitude
            };

            _timer = new Timer(CheckSimulator, state, 0, 2000);
        }

        public void CheckSimulator(object state)
        {
            var expectedLocation = state as ExpectedLocation;

            bool newState = false;

            if (!FSUIPCConnection.IsOpen)
            {
                try
                {
                    FSUIPCConnection.Open();
                }
                catch
                {
                    // TODO: THINK
                }
                newState = FSUIPCConnection.IsOpen;
            } else
            {
                newState = true;
            }

            if(newState != _simConnected)
            {
                _simConnected = newState;
                SimStatusChanged?.Invoke(_simConnected);
            }

            if (_simConnected)
            {
                checkLocationInRange(expectedLocation);
            }
        }

        private void checkLocationInRange(ExpectedLocation expectedLocation)
        {
            bool newStatus = false;

            try
            {
                FSUIPCConnection.Process("Basic");
                longitude = longitudeOffset.Value;
                latitude = latitudeOffset.Value;

                var longitude_diff = Math.Abs(longitude.DecimalDegrees - expectedLocation.AirportLongitude);
                var latitude_diff = Math.Abs(latitude.DecimalDegrees - expectedLocation.AirportLatitude);

                if(longitude_diff <1 && latitude_diff < 1)
                {
                    newStatus = true;
                } else
                {
                    newStatus = false;
                }
            }
            catch
            {
                // TODO: THINK
            }
            if (newStatus != _planeOnAirport)
            {
                _planeOnAirport = newStatus;
                AircraftLocationChanged?.Invoke(_planeOnAirport);
            }
        }

        public class ExpectedLocation
        {
            public double AirportLatitude { get; set; }
            public double AirportLongitude { get; set; }
        }
    }
}
