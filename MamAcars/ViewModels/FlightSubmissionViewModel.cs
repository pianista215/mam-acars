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

        private readonly FsuipcService _fsuipcService;

        private Dictionary<int, string> _chunks = new();

        public event PropertyChangedEventHandler PropertyChanged;

        public int Progress
        {
            get => _progress;
            private set
            {
                _progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        public ICommand StartSubmissionCommand { get; }

        public FlightSubmissionViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _fsuipcService = FsuipcService.Instance;
            StatusMessage = "Starting submission...";
            Progress = 0;
        }

        public async Task SubmitFlightReport()
        {
            List<Func<Task>> steps = new()
                {
                    ExportBlackBox,
                    SplitBlackBox,
                    SendBasicInfo
                };

            int stepProgress = 100 / (steps.Count + 1);

            foreach (var step in steps)
            {
                await step();
                Progress += stepProgress;
            }

            foreach (var chunkId in _chunks.Keys.OrderBy(x => x))
            {
                await UploadChunk(chunkId, _chunks.Count);
                Progress += stepProgress / _chunks.Count;
            }

            await CleanUp();
            Progress = 100;

            OnSubmissionCompleted?.Invoke();
        }

        private async Task ExportBlackBox()
        {
            StatusMessage = "Exporting events to blackbox file...";
            await _fsuipcService.ExportFlightToJson();
        }

        private async Task SplitBlackBox()
        {
            StatusMessage = "Splitting blackbox in pieces...";
            _chunks = await _fsuipcService.SplitBlackBoxData();
        }

        private async Task SendBasicInfo()
        {
            StatusMessage = "Sending basic information...";
            await _fsuipcService.SendBasicInformation();
        }

        private async Task UploadChunk(int i, int totalChunks)
        {
            StatusMessage = $"Uploading black box file {i} of {totalChunks}...";
            await _fsuipcService.SendChunkId(i);
        }

        private async Task CleanUp()
        {
            StatusMessage = "Cleaning up...";
            await _fsuipcService.CleanData();
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event Action OnSubmissionCompleted;
    }
}
