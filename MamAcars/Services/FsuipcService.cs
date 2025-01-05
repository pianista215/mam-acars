using FSUIPC;
using MamAcars.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MamAcars.Services
{
    public class FsuipcService
    {

        private static readonly Lazy<FsuipcService> _instance = new Lazy<FsuipcService>(() => new FsuipcService());

        public static FsuipcService Instance => _instance.Value;

        private FsuipcService()
        {

        }

        private Timer _timer;
        private bool _simConnected = false;
        private bool _planeOnAirport = false;

        public bool SimConnected => _simConnected;

        public bool PlaneOnAirport => _planeOnAirport;

        public event Action<bool> SimStatusChanged;

        public event Action<bool> AircraftLocationChanged;

        private Offset<FsLongitude> longitudeOffset = new Offset<FsLongitude>("Basic", 0x0568, 8);
        private Offset<FsLatitude> latitudeOffset = new Offset<FsLatitude>("Basic", 0x0560, 8);
        private Offset<ushort> onGroundOffset = new Offset<ushort>("Basic", 0x0366);
        private Offset<long> altitudeOffset = new Offset<long>("Basic", 0x0570);


        public void startLookingSimulatorAndAircraftLocation(double airportLatitude, double airportLongitude)
        {
            var state = new ExpectedLocation
            {
                AirportLatitude = airportLatitude,
                AirportLongitude = airportLongitude
            };

            _timer = new Timer(CheckSimulator, state, 0, 2000);
        }

        public void stopLookingSimulatorAndAircraftLocation()
        {
            _timer?.Dispose();
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

            if (newState != _simConnected)
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
                var longitude = longitudeOffset.Value;
                var latitude = latitudeOffset.Value;

                var distance = MamUtils.CalculateDistance(
                    latitude.DecimalDegrees,
                    longitude.DecimalDegrees,
                    expectedLocation.AirportLatitude,
                    expectedLocation.AirportLongitude
                    );

                if (distance <= 8.0) // 8 km of radius from the airport
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

        public class BlackBoxBasicInformation
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }

            public bool onGround { get; set; }

            public int Altitude { get; set; }
        }

        public void startSavingBlackBox()
        {
            _timer = new Timer(SaveBlackBoxData, null, 0, 2000);
        }

        private BlackBoxBasicInformation lastBlackBoxState;

        public void SaveBlackBoxData(object state)
        {
            try
            {
                FSUIPCConnection.Process("Basic");

                BlackBoxBasicInformation blackBoxBasicInformation = new BlackBoxBasicInformation();
                blackBoxBasicInformation.Longitude = longitudeOffset.Value.DecimalDegrees;
                blackBoxBasicInformation.Latitude = latitudeOffset.Value.DecimalDegrees;
                blackBoxBasicInformation.onGround = onGroundOffset.Value > 0;
                blackBoxBasicInformation.Altitude = Convert.ToInt32(altitudeOffset.Value * 3.28084 / (65536.0 * 65536.0));

                Debug.WriteLine($"lat: {blackBoxBasicInformation.Latitude} lon: {blackBoxBasicInformation.Longitude} ground: {blackBoxBasicInformation.onGround} altitude: {blackBoxBasicInformation.Altitude}");
                lastBlackBoxState = blackBoxBasicInformation;
            } catch
            {
                // TODO: THINK
            }
        }



    }
}
