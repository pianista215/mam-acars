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
    public partial class ConfirmFlightPage : Page
    {
        private Action _onSendFlight;
        public ConfirmFlightPage(Action onSendFlight)
        {
            InitializeComponent();
            _onSendFlight = onSendFlight;
        }

        private void OnSendFlightClicked(object sender, RoutedEventArgs e)
        {
            string comment = CommentTextBox.Text;
            _onSendFlight();
        }
    }
}
