using MamAcars.Contracts;
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
    public partial class ConfirmFlightPage : Page, IPageWithUnsavedChanges
    {
        private Action _onSendFlight;
        private readonly ConfirmFlightViewModel _viewModel;
        public bool HasUnsavedChanges { get; private set; }

        public ConfirmFlightPage(Action onSendFlight)
        {
            InitializeComponent();
            _viewModel = new ConfirmFlightViewModel();
            DataContext = _viewModel;
            _onSendFlight = onSendFlight;
            HasUnsavedChanges = true;
        }

        private void OnSendFlightClicked(object sender, RoutedEventArgs e)
        {
            _viewModel.AddComment(CommentTextBox.Text);
            _onSendFlight();
        }
    }
}
