using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace MamAcars.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

        public ApiService()
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

        private async Task<TResponse> SendRequestAsync<TRequest, TResponse>(string url, HttpMethod method, TRequest? requestData = null)
        where TRequest : class
        where TResponse : BaseResponse, new()
        {
            SetDefaultHeaders();
            bool isLoginRequest = url.Contains("v1/auth/login");

            try
            {
                HttpRequestMessage request = new HttpRequestMessage(method, url);
                if (requestData != null)
                {
                    string json = JsonSerializer.Serialize(requestData);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    if (isLoginRequest)
                    {
                        Log.Information($"[API Request] {method} {url} (login obfuscated)");
                    } else
                    {
                        Log.Information($"[API Request] {method} {url}\nPayload: {json}");
                    }
                }
                else
                {
                    Log.Information($"[API Request] {method} {url}");
                }

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();
                if (isLoginRequest)
                {
                    Log.Information($"[API Response] {response.StatusCode} {url} (login obfuscated)");
                } else
                {
                    Log.Information($"[API Response] {response.StatusCode} {url}\nResponse: {responseContent}");
                }

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<TResponse>(responseContent);
                    if (result != null)
                    {
                        result.IsSuccess = true;
                        return result;
                    }
                    return new TResponse { IsSuccess = false, ErrorMessage = "Failed to deserialize response from server." };
                }
                else
                {
                    return HandleErrorResponse<TResponse>(response.StatusCode, responseContent);
                }
            }
            catch (TaskCanceledException)
            {
                Log.Information($"[API Error] Timeout on {method} {url}");
                return new TResponse { IsSuccess = false, ErrorMessage = "Request timed out. Please try again." };
            }
            catch (Exception ex)
            {
                Log.Error($"[API Error] Exception on {method} {url}", ex);
                return new TResponse { IsSuccess = false, ErrorMessage = $"An unexpected error occurred: {ex.Message}" };
            }
        }

        private TResponse HandleErrorResponse<TResponse>(System.Net.HttpStatusCode statusCode, string responseContent)
            where TResponse : BaseResponse, new()
        {
            Log.Information($"[API Error] {statusCode}: {responseContent}");

            if (statusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return new TResponse { IsSuccess = false, AuthFailure = true, ErrorMessage = "Unauthorized access. Please check your credentials." };
            }
            if (statusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new TResponse { IsSuccess = false, ErrorMessage = "Resource not found." };
            }

            var errorData = JsonSerializer.Deserialize<GenericErrorResponse>(responseContent);
            return new TResponse { IsSuccess = false, ErrorMessage = $"Server: {errorData?.message ?? "Unknown error"}" };
        }

        public async Task<LoginResponse> LoginAsync(string license, string password)
        {
            var data = new LoginRequest { license = license, password = password };
            return await SendRequestAsync<LoginRequest, LoginResponse>("v1/auth/login", HttpMethod.Post, data);
        }

        public async Task<FlightPlanInfoResponse> CurrentFlightPlanAsync()
        {
            return await SendRequestAsync<object, FlightPlanInfoResponse>("v1/flight-plan/current-fpl", HttpMethod.Get);
        }

        public async Task<SubmitReportResponse> SubmitReportAsync(long flightPlanId, SubmitReportRequest rq)
        {
            string url = $"v1/flight-report/submit-report?flight_plan_id={flightPlanId}";
            return await SendRequestAsync<SubmitReportRequest, SubmitReportResponse>(url, HttpMethod.Post, rq);
        }

        public async Task<UploadChunkResponse> UploadChunk(string flightReportId, int chunkId, string chunkPath)
        {
            try
            {
                var content = new MultipartFormDataContent();
                using var stream = File.OpenRead(chunkPath);
                var streamContent = new StreamContent(stream);
                content.Add(streamContent, "chunkFile", Path.GetFileName(chunkPath));

                Log.Information($"[API Request] POST v1/flight-report/upload-chunk?flight_report_id={flightReportId}&chunk_id={chunkId}\nUploading file: {chunkPath}");

                HttpResponseMessage response = await _httpClient.PostAsync(
                    $"v1/flight-report/upload-chunk?flight_report_id={flightReportId}&chunk_id={chunkId}",
                    content
                );

                string responseContent = await response.Content.ReadAsStringAsync();
                Log.Information($"[API Response] {response.StatusCode} UploadChunk\nResponse: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<UploadChunkResponse>(responseContent);
                    if(result != null)
                    {
                        result.IsSuccess = true;
                        return result;
                    } else
                    {
                        new UploadChunkResponse { IsSuccess = false, ErrorMessage = "Failed to deserialize response from server." };
                    }
                }
                return HandleErrorResponse<UploadChunkResponse>(response.StatusCode, responseContent);
            }
            catch (Exception ex)
            {
                Log.Error($"[API Error] Exception on UploadChunk", ex);
                return new UploadChunkResponse { IsSuccess = false, ErrorMessage = $"An unexpected error occurred: {ex.Message}" };
            }
        }

    }

    public class BaseResponse
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public bool AuthFailure { get; set; } = false;
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

    public class LoginResponse : BaseResponse
    {
        public string access_token { get; set; }
    }

    public class FlightPlanInfoResponse : BaseResponse
    {
        public bool EmptyFlightPlan { get; set; } = false;
        public long id { get; set; }
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

    public class SubmitReportResponse : BaseResponse
    {
        public string flight_report_id { get; set; }
    }

    public class UploadChunkResponse : BaseResponse
    {
        public string status { get; set; }
    }

}
