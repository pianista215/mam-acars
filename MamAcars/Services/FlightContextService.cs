using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MamAcars.Services
{
    public class FlightContextService
    {

        private static readonly Lazy<FlightContextService> _instance = new Lazy<FlightContextService>(() => new FlightContextService());

        public static FlightContextService Instance => _instance.Value;

        private readonly ApiService _apiService;
        private readonly FlightEventStorage _storage;
        private readonly FsuipcService _fsuipcService;

        private FlightPlanInfoResponse flightPlan;

        private FlightContextService()
        {
            _apiService = new ApiService();
            _storage = new FlightEventStorage();
            _fsuipcService = new FsuipcService(_storage);
        }

        public Boolean existStoredCredentials()
        {
            var token = TokenStorage.GetToken();
            return !string.IsNullOrEmpty(token);
        }

        public async Task<LoginResponse> Login(string license, string password)
        {
            var response = await _apiService.LoginAsync(license, password);
            if (response.IsSuccess)
            {
                TokenStorage.SaveToken(response.access_token);
            }
            return response;
        }

        public async Task<FlightPlanInfoResponse> LoadCurrentFlightPlan()
        {
            _apiService.SetBearerToken(TokenStorage.GetToken());

            var flightInfo = await _apiService.CurrentFlightPlanAsync();
            if(flightInfo != null)
            {
                if (flightInfo.IsSuccess)
                {
                    this.flightPlan = flightInfo;
                } else if (flightInfo.AuthFailure)
                {
                    TokenStorage.DeleteToken();
                }
            }

            return flightInfo;
        }

        public void startMonitoringSimulator(Action<bool> SimStatusChanged, Action<bool> AircraftLocationChanged)
        {
            _fsuipcService.SimStatusChanged += SimStatusChanged;
            _fsuipcService.AircraftLocationChanged += AircraftLocationChanged;

            _fsuipcService.startLookingSimulatorAndAircraftLocation(flightPlan.departure_latitude, flightPlan.departure_longitude);
        }

        public void startSavingBlackBox()
        {
            _fsuipcService.startSavingBlackBox(flightPlan.id);
        }

        public void stopSavingBlackBox()
        {
            _fsuipcService.stopSavingBlackBox();
        }

        public void SetCommentToBlackBox(string comment)
        {
            _storage.SetComment(flightPlan.id, comment);
        }

        public async Task ExportFlightToJson()
        {
            _storage.ExportCurrentFlightToJson(flightPlan.id);
        }

        public async Task<Dictionary<int, string>> SplitBlackBoxData()
        {
            return await _storage.SplitBlackBoxData(flightPlan.id);
        }

    }
}
