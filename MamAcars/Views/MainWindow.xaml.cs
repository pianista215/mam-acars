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

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            ShowLoginPage();
        }

        private void ShowLoginPage()
        {
            MainFrame.Navigate(new LoginPage(OnLoginSuccess));
        }

        private void OnLoginSuccess()
        {
            MainFrame.Navigate(new FlightInfoPage(OnStartFlight));
        }

        private void OnStartFlight()
        {
            MainFrame.Navigate(new FlightRecordingPage(OnEndFlight));
        }

        private void OnEndFlight()
        {
            MainFrame.Navigate(new ConfirmFlightPage(OnSendFlight));
        }

        private void OnSendFlight()
        {
            MainFrame.Navigate(new FlightConfirmedPage());
        }
    }
}
