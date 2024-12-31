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

        public FlightInfoPage(Action onStartFlight)
        {
            InitializeComponent();
            _onStartFlight = onStartFlight;
        }

        private void OnStartFlight(object sender, RoutedEventArgs e)
        {
            _onStartFlight();
        }
    }
}
