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
            Loaded += async (s, e) => await _viewModel.SubmitFlightReport();
        }

        private void NavigateToFlightConfirmedPage()
        {
            Dispatcher.Invoke(() =>
            {
                if (NavigationService != null)
                {
                    NavigationService.Navigate(new FlightConfirmedPage());
                }
            });
        }
    }
}
