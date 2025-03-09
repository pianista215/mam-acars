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

    public partial class FlightSubmissionPage : Page
    {
        private readonly FlightSubmissionViewModel _viewModel;

        public FlightSubmissionPage(Action onSubmissionCompleted)
        {
            InitializeComponent();
            _viewModel = new FlightSubmissionViewModel();
            DataContext = _viewModel;
            _viewModel.OnSubmissionCompleted += onSubmissionCompleted;
            Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            this.submitFlightReport();
        }

        private async void submitFlightReport()
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            await _viewModel.SubmitFlightReport();
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Arrow;
        }

        private async void OnRetryClicked(object sender, RoutedEventArgs e)
        {
            this.submitFlightReport();
        }
    }
}
