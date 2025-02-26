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
    public partial class FlightInfoPage : Page
    {
        private Action _onStartFlight;
        private readonly FlightInfoViewModel _viewModel;


        public FlightInfoPage(Action onStartFlight)
        {
            InitializeComponent();
            _viewModel = new FlightInfoViewModel();
            DataContext = _viewModel;
            Loaded += OnPageLoaded;
            _onStartFlight = onStartFlight;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            _viewModel.LoadFlightInfo();
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Arrow;
        }

        private void OnStartFlight(object sender, RoutedEventArgs e)
        {
            if (_viewModel.AuthFailure)
            {
                NavigationService.GoBack();
            } else if (_viewModel.CloseOnClick)
            {
                Application.Current.Shutdown();
            }
            else
            {
                _onStartFlight();
            }
        }
    }
}
