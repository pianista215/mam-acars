using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MamAcars.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

        public ApiService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:8080/"),
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        public async Task<LoginResponse> LoginAsync(string license, string password)
        {
            try
            {
                var data = new
                {
                    license,
                    password
                };

                var json = JsonSerializer.Serialize(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("v1/auth/login", content);

                // Verifica si la respuesta es exitosa
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<LoginResponse>(responseContent) ?? new LoginResponse { IsSuccess = false };
                }
                else
                {
                    return new LoginResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Error: {response.StatusCode}"
                    };
                }
            }
            catch (TaskCanceledException)
            {
                // Controla el timeout
                return new LoginResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Request timed out. Please try again."
                };
            }
            catch (Exception ex)
            {
                // Controla cualquier otro error (e.g., deserialización fallida)
                return new LoginResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"An unexpected error occurred: {ex.Message}"
                };
            }
        }
    
    }

    public class LoginResponse
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public string Token { get; set; }
    }
}
