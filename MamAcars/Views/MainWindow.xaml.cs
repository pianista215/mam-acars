using MamAcars.Contracts;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Reflection;

namespace MamAcars
{

    public partial class MainWindow : Window
    {
        private bool forceExit = false;
        public MainWindow()
        {
            InitializeComponent();
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Title = $"{BrandingConfig.AppName} v{version?.Major}.{version?.Minor}.{version?.Build}";

            this.Closing += MainWindow_Closing;

            ShowLoginPage();
        }

        private IPageWithUnsavedChanges GetCurrentPage()
        {
            return MainFrame.Content as IPageWithUnsavedChanges;
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (forceExit)
                return;

            var page = GetCurrentPage();

            if (page != null && page.HasUnsavedChanges)
            {
                var res = MessageBox.Show(
                    "The progress of your flight will be lost if you exit now.\n\nAre you sure you want to exit?",
                    "Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (res == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
                else
                {
                    forceExit = true;
                    Application.Current.Shutdown();
                }
            }
        }

        private void ShowLoginPage()
        {
            MainFrame.Navigate(new LoginPage(OnLoginSuccess, CheckPendingSubmissions));
        }

        private void CheckPendingSubmissions()
        {
            MainFrame.Navigate(new FlightSubmissionPage(OnSendFlight));
        }

        private async void OnLoginSuccess()
        {
            await App.CheckForUpdates();
            MainFrame.Navigate(new FlightInfoPage(OnStartFlight));
        }

        private void OnStartFlight()
        {
            MainFrame.Navigate(new FlightRecordingPage(OnEndFlight));
        }

        private void OnEndFlight()
        {
            MainFrame.Navigate(new ConfirmFlightPage(OnSubmitFlight));
        }

        private void OnSubmitFlight()
        {
            MainFrame.Navigate(new FlightSubmissionPage(OnSendFlight));
        }

        private void OnSendFlight()
        {
            MainFrame.Navigate(new FlightConfirmedPage());
        }
    }
}
