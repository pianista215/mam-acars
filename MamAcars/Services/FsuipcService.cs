using FSUIPC;
using MamAcars.Models;
using MamAcars.Utils;
using Microsoft.VisualBasic;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MamAcars.Services
{
    public class FsuipcService
    {

        private FlightEventStorage _storage;

        public FsuipcService(FlightEventStorage storage)
        {
            _storage = storage; 
        }

        private Timer _timer;
        private bool _simConnected = false;
        private bool _planeOnAirport = false;

        public bool SimConnected => _simConnected;

        public bool PlaneOnAirport => _planeOnAirport;

        public event Action<bool> SimStatusChanged;

        public event Action<bool> AircraftLocationChanged;

        private const string BASIC_OFFSET = "Basic";

        private Offset<FsLongitude> longitudeOffset = new Offset<FsLongitude>(BASIC_OFFSET, 0x0568, 8);
        private Offset<FsLatitude> latitudeOffset = new Offset<FsLatitude>(BASIC_OFFSET, 0x0560, 8);
        private Offset<ushort> onGroundOffset = new Offset<ushort>(BASIC_OFFSET, 0x0366);
        private Offset<long> altitudeOffset = new Offset<long>(BASIC_OFFSET, 0x0570);
        private Offset<int> groundAltitudeOffset = new Offset<int>(BASIC_OFFSET, 0x0020);
        private Offset<uint> headingOffset = new Offset<uint>(BASIC_OFFSET, 0x0580);
        private Offset<short> magneticVariationOffset = new Offset<short>(BASIC_OFFSET, 0x2A0);
        private Offset<int> groundSpeedOffset = new Offset<int>(BASIC_OFFSET, 0x02B4);


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

        public void startSavingBlackBox(long flightPlanId)
        {
            Log.Information($"Start saving blackbox fplId {flightPlanId}");
            _storage.RegisterFlight(flightPlanId, "xplane_aircraft_xxx");
            _timer = new Timer(SaveBlackBoxData, flightPlanId, 0, 2000);
        }

        public void stopSavingBlackBox()
        {
            Log.Information("Stop saving blackbox");
            _timer?.Dispose();
        }

        private double RoundToDecimals(double value, int decimals)
        {
            return Math.Round(value, decimals);
        }

        public void SaveBlackBoxData(object state)
        {
            try
            {
                FSUIPCConnection.Process("Basic");

                BlackBoxBasicInformation blackBoxBasicInformation = new BlackBoxBasicInformation();
                blackBoxBasicInformation.Longitude = RoundToDecimals(longitudeOffset.Value.DecimalDegrees, 5);
                blackBoxBasicInformation.Latitude = RoundToDecimals(latitudeOffset.Value.DecimalDegrees, 5);
                blackBoxBasicInformation.onGround = onGroundOffset.Value > 0;
                blackBoxBasicInformation.Altitude = Convert.ToInt32(altitudeOffset.Value * MamUtils.METER_TO_FEETS / (65536.0 * 65536.0));
                int groundAltitudeFeets = (int)((double)groundAltitudeOffset.Value / 256.0 * MamUtils.METER_TO_FEETS);

                blackBoxBasicInformation.AGLAltitude = blackBoxBasicInformation.Altitude - groundAltitudeFeets;

                var heading = (double) headingOffset.Value * 360.0 / (65536.0 * 65536.0);
                var magneticVariation = (double) magneticVariationOffset.Value * 360.0 / 65536.0;
                blackBoxBasicInformation.Heading = (int) (heading - magneticVariation) % 360;

                blackBoxBasicInformation.GroundSpeedKnots = (int)((double)groundSpeedOffset.Value * 3600d / 65536d / 1852d);

                var flightId = state as long?;

                _storage.RecordEvent(flightId, blackBoxBasicInformation);
            } catch
            {
                // TODO: THINK
            }
        }

    }
}
