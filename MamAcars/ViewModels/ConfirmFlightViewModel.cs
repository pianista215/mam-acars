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
        private readonly FsuipcService _fsuipcService;

        public ConfirmFlightViewModel()
        {
            _fsuipcService = FsuipcService.Instance;
        }

        public void AddComment(string comment)
        {
            _fsuipcService.SetCommentToBlackBox(comment);
        }

    }
}
