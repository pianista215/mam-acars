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
    public partial class FlightRecordingPage : Page
    {
        private Action _onEndFlight;

        public FlightRecordingPage(Action onEndFlight)
        {
            InitializeComponent();
            _onEndFlight = onEndFlight;
        }

        private void OnEndFlightClicked(object sender, RoutedEventArgs e)
        {
            _onEndFlight();
        }
    }
}
