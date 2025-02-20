using MamAcars.Services;
using MamAcars.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MamAcars
{
    public partial class LoginPage : Page
    {
        private Action _onLoginSuccess;
        private Action _onPendingSubmission;

        private LoginViewModel _viewModel;

        public LoginPage(Action onLoginSuccess, Action onPendingSubmission)
        {
            InitializeComponent();
            _viewModel = new LoginViewModel();
            DataContext = _viewModel;
            _viewModel.OnLoginSuccess += onLoginSuccess;
            _onLoginSuccess = onLoginSuccess;
            _onPendingSubmission = onPendingSubmission;
        }

        private void disableComponents()
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            LoginBtn.IsEnabled = false;
            LicenseTextbox.IsEnabled = false;
            PasswordBox.IsEnabled = false;
            ErrorTextBlock.Visibility = Visibility.Hidden;
        }

        private void enableComponents()
        {
            LoginBtn.IsEnabled = true;
            LicenseTextbox.IsEnabled = true;
            PasswordBox.IsEnabled = true;
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Arrow;
        }

        private void AskForPendingFlightDataUpload()
        {
            var result = MessageBox.Show("You have one flight with pending data to be uploaded. Do you want to upload it? Otherwise the data will be deleted",
                             "Pending flight data to upload",
                             MessageBoxButton.YesNo,
                             MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _onPendingSubmission?.Invoke();
            }
            else
            {
                _viewModel.CleanPreviousData();
                _onLoginSuccess?.Invoke();
            }
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            disableComponents();
            if (_viewModel.ExistsPreviousLoginToken())
            {
                bool validAuth = await _viewModel.LoginWithExistingCredentials();
                if (validAuth)
                {
                    if (_viewModel.ExistsPendingFlightToBeSubmitted())
                    {
                        AskForPendingFlightDataUpload();
                    }
                    else
                    {
                        _onLoginSuccess?.Invoke();
                    }
                } else
                {
                    enableComponents();
                }
               
            } else
            {
                enableComponents();
            }
        }

        private async void OnLoginClicked(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(LicenseTextbox.Text) && !string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                disableComponents();

                _viewModel.License = LicenseTextbox.Text;
                _viewModel.Password = PasswordBox.Password;
                ErrorTextBlock.Visibility = Visibility.Hidden;

                bool success = await _viewModel.Login();

                if (success)
                {
                    if (_viewModel.ExistsPendingFlightToBeSubmitted())
                    {
                        AskForPendingFlightDataUpload();
                    }
                    else
                    {
                        _onLoginSuccess?.Invoke();
                    }
                }
                else
                {
                    ErrorTextBlock.Text = _viewModel.ErrorMessage;
                    ErrorTextBlock.Visibility = Visibility.Visible;
                    enableComponents();
                }
            }
        }
    }
}
