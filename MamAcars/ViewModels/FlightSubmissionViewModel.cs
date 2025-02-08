using MamAcars.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace MamAcars.ViewModels
{
    public class FlightSubmissionViewModel : INotifyPropertyChanged
    {
        private int _progress;
        private string _statusMessage;
        private readonly Dispatcher _dispatcher;

        private readonly FlightContextService _contextService;

        private int numberOfChunks;

        public event PropertyChangedEventHandler PropertyChanged;

        public int Progress
        {
            get => _progress;
            set
            {
                _dispatcher.Invoke(() =>
                {
                    _progress = value;
                    OnPropertyChanged(nameof(Progress));
                });
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        public ICommand StartSubmissionCommand { get; }

        public FlightSubmissionViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _contextService = FlightContextService.Instance;
            StatusMessage = "Starting submission...";
            Progress = 0;
        }

        public async Task SubmitFlightReport()
        {
            List<Func<Task>> steps = new()
                {
                    ExportBlackBox,
                    SplitBlackBox,
                    SendFlightReport
                };

            int stepProgress = 100 / (steps.Count + 1);

            foreach (var step in steps)
            {
                await step();
                Progress += stepProgress;
            }

            for (int i=0; i < numberOfChunks; i++)
            {
                await UploadChunk(i, numberOfChunks);
                Progress += stepProgress / numberOfChunks;
            }

            await CleanUp();
            Progress = 100;

            OnSubmissionCompleted?.Invoke();
        }

        // TODO: RETRIES/FAILURES MANAGEMENT

        private async Task ExportBlackBox()
        {
            StatusMessage = "Exporting events to blackbox file...";
            await _contextService.ExportFlightToJson();
        }

        private async Task SplitBlackBox()
        {
            StatusMessage = "Splitting blackbox in pieces...";
            numberOfChunks = await _contextService.SplitBlackBoxData();
        }

        private async Task SendFlightReport()
        {
            StatusMessage = "Sending basic information...";
            await _contextService.SendFlightReport();
        }

        private async Task UploadChunk(int i, int totalChunks)
        {
            StatusMessage = $"Uploading black box file {i + 1} of {totalChunks}...";
            await _contextService.UploadChunk(i);
        }

        private async Task CleanUp()
        {
            StatusMessage = "Cleaning up...";
            await _contextService.CleanData();
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event Action OnSubmissionCompleted;
    }
}
