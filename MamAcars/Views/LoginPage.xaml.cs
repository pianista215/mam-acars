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

        private async void OnLoginClicked(object sender, RoutedEventArgs e)
        {
            if (await _viewModel.Login())
            {
                // Login successful, call the onLoginSuccess action (passed in from outside)
                _onLoginSuccess();
            }
            else
            {
                // Show error message to the user
                MessageBox.Show("Login failed!");
            }
        }
    }
}
