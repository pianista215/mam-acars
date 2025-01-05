using MamAcars.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Xps;

namespace MamAcars.ViewModels
{
    public class FlightInfoViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private readonly FsuipcService _fsuipcService;
        private string _departureAirport;
        private string _arrivalAirport;
        private string _alternateAirports;
        private string _aircraftDetails;
        private string _errorMessage;
        private string _startFlightBtnText = "Start Flight";
        private bool _startFlightBtnEnabled = false;
        private Visibility _errorVisibility = Visibility.Collapsed;
        private Visibility _flightInfoVisibility = Visibility.Collapsed;

        public bool AuthFailure { get; set; } = false;

        public bool CloseOnClick { get; set; } = false;

        public event PropertyChangedEventHandler PropertyChanged;

        public FlightInfoViewModel()
        {
            _apiService = ApiService.Instance;
            _fsuipcService = FsuipcService.Instance;
            _fsuipcService.SimStatusChanged += OnSimStatusChanged;
            _fsuipcService.AircraftLocationChanged += OnAircraftLocationChanged;

            // TODO: THINK ABOUT TOKEN SET
            _apiService.SetBearerToken(TokenStorage.GetToken());
        }

        public string DepartureAirport
        {
            get => _departureAirport;
            set => SetProperty(ref _departureAirport, value);
        }

        public string ArrivalAirport
        {
            get => _arrivalAirport;
            set => SetProperty(ref _arrivalAirport, value);
        }

        public string AlternateAirports
        {
            get => _alternateAirports;
            set => SetProperty(ref _alternateAirports, value);
        }

        public string AircraftDetails
        {
            get => _aircraftDetails;
            set => SetProperty(ref _aircraftDetails, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public string StartFlightBtnText
        {
            get => _startFlightBtnText;
            set => SetProperty(ref _startFlightBtnText, value);
        }

        public bool StartFlightBtnEnabled
        {
            get => _startFlightBtnEnabled;
            set => SetProperty(ref _startFlightBtnEnabled, value);
        }

        public Visibility ErrorVisibility
        {
            get => _errorVisibility;
            set => SetProperty(ref _errorVisibility, value);
        }

        public Visibility FlightInfoVisibility
        {
            get => _flightInfoVisibility;
            set => SetProperty(ref _flightInfoVisibility, value);
        }

        private void OnSimStatusChanged(bool isSimConnected)
        {
            if (isSimConnected)
            {
                StartFlightBtnText = $"Waiting aircraft to be in {_departureAirport}";
                StartFlightBtnEnabled = false;
            } else
            {
                StartFlightBtnText = "Detecting simulator";
                StartFlightBtnEnabled = false;
            }
        }

        private void OnAircraftLocationChanged(bool isAircraftAtStartLocation)
        {
            if (isAircraftAtStartLocation)
            {
                StartFlightBtnText = "Start Flight";
                StartFlightBtnEnabled = true;
            }
            else
            {
                StartFlightBtnText = $"Waiting aircraft to be in {_departureAirport}";
                StartFlightBtnEnabled = false;
            }
        }

        public async Task LoadFlightInfoAsync()
        {
            try
            {
                var flightInfo = await _apiService.CurrentFlightPlanAsync();

                if (flightInfo == null)
                {
                    ShowErrorMessage("Something strange happened retrieving current flight plan. Contact administrator.");
                    return;
                } else
                {
                    if (flightInfo.IsSuccess)
                    {
                        DepartureAirport = flightInfo.departure_icao;
                        ArrivalAirport = flightInfo.arrival_icao;
                        AlternateAirports = string.IsNullOrEmpty(flightInfo.alt2_icao) ? flightInfo.alt1_icao : $"{flightInfo.alt1_icao}, {flightInfo.alt2_icao}";
                        AircraftDetails = $"{flightInfo.aircraft_type_icao} ({flightInfo.aircraft_reg})";
                        FlightInfoVisibility = Visibility.Visible;
                        ErrorVisibility = Visibility.Collapsed;
                        StartFlightBtnText = "Detecting simulator";
                        StartFlightBtnEnabled = false;
                        _fsuipcService.startMonitoring(flightInfo.departure_latitude, flightInfo.departure_longitude);
                    } else
                    {
                        if (flightInfo.AuthFailure)
                        {
                            TokenStorage.DeleteToken();
                            ShowErrorMessage("There was a problem with your credentials, please login again");
                            AuthFailure = true;
                            StartFlightBtnText = "Relogin";
                            StartFlightBtnEnabled = true;
                        } else if (flightInfo.EmptyFlightPlan)
                        {
                            ShowErrorMessage("It appears you haven't submitted a flight plan. Please send one and try again.");
                            CloseOnClick = true;
                            StartFlightBtnText = "Close";
                            StartFlightBtnEnabled = true;
                        } else
                        {
                            ShowErrorMessage(flightInfo.ErrorMessage);
                            CloseOnClick = true;
                            StartFlightBtnText = "Close";
                            StartFlightBtnEnabled = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"An unexpected error occurred: {ex.Message}");
            }
        }

        private void ShowErrorMessage(string message)
        {
            ErrorMessage = message;
            ErrorVisibility = Visibility.Visible;
            FlightInfoVisibility = Visibility.Collapsed;
        }

        private void SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
