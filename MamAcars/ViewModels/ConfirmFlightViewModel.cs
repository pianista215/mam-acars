using MamAcars.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MamAcars.ViewModels
{
    public class ConfirmFlightViewModel
    {
        private readonly FlightContextService _contextService;

        public ConfirmFlightViewModel()
        {
            _contextService = FlightContextService.Instance;
        }

        public void AddComment(string comment)
        {
            _contextService.SetCommentToBlackBox(comment);
        }

    }
}
