﻿using MamAcars.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
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

        private readonly FlightContextService _contextService;

        public event Action OnLoginSuccess;

        public LoginViewModel()
        {
            _contextService = FlightContextService.Instance;
        }

        public Boolean ExistsPreviousLoginToken()
        {
            return _contextService.ExistStoredCredentials();
        }

        public Boolean ExistsPendingFlightToBeSubmitted()
        {
            return _contextService.ExistsPendingFlightToBeSubmitted();
        }

        public void PreparePendingDataForSubmit()
        {
            _contextService.LoadPendingDataForSubmit();
        }

        public void CleanPreviousData()
        {
            _contextService.CleanPreviousData();
        }

        public async Task<bool> Login()
        {
            var response = await _contextService.Login(License, Password);

            if (response.IsSuccess)
            {
                IsErrorVisible = false;
                await _contextService.LoadCurrentFlightPlan();
                return true;
            }
            else
            {
                ErrorMessage = response.ErrorMessage ?? "Unexpected error. Contact your administrator.";
                IsErrorVisible = true;
                return false;
            }
        }

        public async Task<bool> LoginWithExistingCredentials()
        {
            var response = await _contextService.LoadCurrentFlightPlan();

            return response != null && !response.AuthFailure;
        }
    }
}
