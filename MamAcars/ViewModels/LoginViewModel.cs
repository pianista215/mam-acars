using MamAcars.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MamAcars.ViewModels
{
    public class LoginViewModel
    {
        public string License { get; set; }
        public string Password { get; set; }

        public string ErrorMessage { get; set; }
        public bool IsErrorVisible { get; private set; }

        private readonly ApiService _apiService;
        public LoginViewModel()
        {
            _apiService = new ApiService();
        }

        public async Task<bool> Login()
        {
            var response = await _apiService.LoginAsync(License, Password);

            if (response.IsSuccess)
            {
                IsErrorVisible = false;
                return true;
            }
            else
            {
                ErrorMessage = response.ErrorMessage ?? "Unexpected error. Contact your administrator.";
                IsErrorVisible = true;
                return false;
            }
        }
    }
}
