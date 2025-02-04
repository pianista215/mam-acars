using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace MamAcars.Services
{
    public class ApiService
    {
        private static readonly Lazy<ApiService> _instance = new Lazy<ApiService>(() => new ApiService());

        public static ApiService Instance => _instance.Value;

        private readonly HttpClient _httpClient;

        private ApiService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:8080/api/"),
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        public void SetBearerToken(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        private void SetDefaultHeaders()
        {
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<LoginResponse> LoginAsync(string license, string password)
        {
            SetDefaultHeaders();
            try
            {
                var data = new LoginRequest
                {
                    license = license,
                    password = password
                };

                var json = JsonSerializer.Serialize(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("v1/auth/login", content);

                // Verifica si la respuesta es exitosa
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseContent);

                    if(loginResponse != null)
                    {
                        loginResponse.IsSuccess = true;
                        return loginResponse;
                    } else
                    {
                        return new LoginResponse
                        {
                            IsSuccess = false,
                            ErrorMessage = "Failed to deserialize response from server."
                        };
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorData = JsonSerializer.Deserialize<GenericErrorResponse>(errorContent);

                    return new LoginResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Server: {errorData.message}"
                    };
                }
            }
            catch (TaskCanceledException)
            {
                return new LoginResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Request timed out. Please try again."
                };
            }
            catch (Exception ex)
            {
                return new LoginResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"An unexpected error occurred: {ex.Message}"
                };
            }
        }

        public async Task<FlightPlanInfoResponse> CurrentFlightPlanAsync()
        {
            SetDefaultHeaders();
            try
            {

                var response = await _httpClient.GetAsync("v1/flight-plan/current-fpl");

                // Verifica si la respuesta es exitosa
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var flightInfoResponse = JsonSerializer.Deserialize<FlightPlanInfoResponse>(responseContent);

                    if (flightInfoResponse != null)
                    {
                        flightInfoResponse.IsSuccess = true;
                        return flightInfoResponse;
                    }
                    else
                    {
                        return new FlightPlanInfoResponse
                        {
                            IsSuccess = false,
                            ErrorMessage = "Failed to deserialize response from server."
                        };
                    }
                }
                else
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        return new FlightPlanInfoResponse
                        {
                            IsSuccess = false,
                            AuthFailure = true
                        };
                    } else if(response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return new FlightPlanInfoResponse
                        {
                            IsSuccess = false,
                            EmptyFlightPlan = true
                        };
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        var errorData = JsonSerializer.Deserialize<GenericErrorResponse>(errorContent);

                        return new FlightPlanInfoResponse
                        {
                            IsSuccess = false,
                            ErrorMessage = $"Server: {errorData.message}"
                        };
                    }
                }
            }
            catch (TaskCanceledException)
            {
                return new FlightPlanInfoResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Request timed out. Please try again."
                };
            }
            catch (Exception ex)
            {
                return new FlightPlanInfoResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"An unexpected error occurred: {ex.Message}"
                };
            }
        }

        public async Task<SubmitReportResponse> SubmitReportAsync(ulong flightPlanId, SubmitReportRequest rq)
        {
            SetDefaultHeaders();
            try
            {

                var json = JsonSerializer.Serialize(rq);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"v1/flight-report/submit-report?flight_plan_id={flightPlanId}", content);

                // Verifica si la respuesta es exitosa
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var submitReportResponse = JsonSerializer.Deserialize<SubmitReportResponse>(responseContent);

                    if (submitReportResponse != null)
                    {
                        submitReportResponse.IsSuccess = true;
                        return submitReportResponse;
                    }
                    else
                    {
                        return new SubmitReportResponse
                        {
                            IsSuccess = false,
                            ErrorMessage = "Failed to deserialize response from server."
                        };
                    }
                }
                else
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        return new SubmitReportResponse
                        {
                            IsSuccess = false,
                            ErrorMessage = "Problem with authentication while closing flight plan. Try later"
                        };
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return new SubmitReportResponse
                        {
                            IsSuccess = false,
                            ErrorMessage = "Can't find the submitted flight plan. Have you delete it?"
                        };
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        var errorData = JsonSerializer.Deserialize<GenericErrorResponse>(errorContent);

                        return new SubmitReportResponse
                        {
                            IsSuccess = false,
                            ErrorMessage = $"Server: {errorData.message}"
                        };
                    }
                }
            }
            catch (TaskCanceledException)
            {
                return new SubmitReportResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Request timed out. Please try again."
                };
            }
            catch (Exception ex)
            {
                return new SubmitReportResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"An unexpected error occurred: {ex.Message}"
                };
            }
        }

    }

    public class GenericErrorResponse
    {
        public string message { get; set; }
    }

    public class LoginRequest
    {
        public string license { get; set; }
        public string password { get; set; }
    }

    public class LoginResponse
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public string access_token { get; set; }
    }

    public class FlightPlanInfoResponse
    {
        public bool IsSuccess { get; set; }

        public bool AuthFailure { get; set; } = false;

        public bool EmptyFlightPlan { get; set; } = false;

        public string ErrorMessage { get; set; }

        public ulong id { get; set; }
        public string departure_icao { get; set; }
        public double departure_latitude { get; set; }
        public double departure_longitude { get; set; }
        public string arrival_icao { get; set; }
        public string alt1_icao { get; set; }
        public string alt2_icao { get; set; }
        public string aircraft_type_icao { get; set; }
        public string aircraft_reg { get; set; }
    }

    public class SubmitReportRequest
    {
        public string pilot_comments { get; set; }
        public double last_position_lat { get; set; }
        public double last_position_lon { get; set; }

        public string network { get; set; }
        public string sim_aircraft_name { get; set; }
        public string report_tool { get; set; }
        public string start_time { get; set; }
        public string end_time { get; set; }

        public AcarsChunks[] chunks { get; set; }
    }

    public class AcarsChunks
    {
        public int id { get; set; }
        public string sha256sum { get; set; }
    }

    public class SubmitReportResponse
    {
        public ulong flight_report_id { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
    }
    
}
