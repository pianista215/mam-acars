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

        public LoginPage(Action onLoginSuccess)
        {
            InitializeComponent();
            _onLoginSuccess = onLoginSuccess;
        }

        private void OnLoginClicked(object sender, RoutedEventArgs e)
        {
            // Aquí haces la autenticación, si es exitosa:
            _onLoginSuccess();
        }
    }
}
