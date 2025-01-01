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

        private LoginViewModel _viewModel;

        public LoginPage(Action onLoginSuccess)
        {
            InitializeComponent();
            _viewModel = new LoginViewModel();
            DataContext = _viewModel;
            _onLoginSuccess = onLoginSuccess;
        }

        private void disableComponents()
        {
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
        }

        private async void OnLoginClicked(object sender, RoutedEventArgs e)
        {
            disableComponents();

            _viewModel.License = LicenseTextbox.Text;
            _viewModel.Password = PasswordBox.Password;
            ErrorTextBlock.Visibility = Visibility.Hidden;

            bool success = await _viewModel.Login();

            if (success)
            {
                // TODO: SAVE TOKEN
                _onLoginSuccess();
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
