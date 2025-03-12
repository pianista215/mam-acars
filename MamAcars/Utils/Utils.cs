using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MamAcars.Utils
{
    public static class MamUtils
    {

        public static string GetAppNameAndVersion()
        {
            string appName = Assembly.GetExecutingAssembly().GetName().Name ?? "MamAcars";
            string version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "Unknown";

            return $"{appName} {version}";
        }

        private const double EarthRadiusKm = 6371.0;

        public const double METER_TO_FEETS = 3.28084;

        public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            double lat1Rad = DegreesToRadians(lat1);
            double lon1Rad = DegreesToRadians(lon1);
            double lat2Rad = DegreesToRadians(lat2);
            double lon2Rad = DegreesToRadians(lon2);

            double deltaLat = lat2Rad - lat1Rad;
            double deltaLon = lon2Rad - lon1Rad;

            double a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                       Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                       Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EarthRadiusKm * c;
        }

        public static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        public static string GetOnlineNetwork()
        {
            var processes = Process.GetProcesses()
            .Select(p => p.ProcessName.ToLower())
            .ToList();

            if (processes.Contains("altitudex") || processes.Contains("pilotui"))
            {
                return "IVAO";
            }
            else if (processes.Contains("vpilot") || processes.Contains("xpilot"))
            {
                return "VATSIM";
            }
            else
            {
                return "UNKNOWN";
            }
        }
    }
}
