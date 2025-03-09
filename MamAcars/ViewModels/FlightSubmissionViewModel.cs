using MamAcars.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace MamAcars.ViewModels
{
    public class FlightSubmissionViewModel : INotifyPropertyChanged
    {
        private int _progress;
        private string _statusMessage;
        private Visibility _retryBtnVisible;
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

        public Visibility RetryBtnVisible
        {
            get => _retryBtnVisible;
            set
            {
                _retryBtnVisible = value;
                OnPropertyChanged(nameof(RetryBtnVisible));
            }
        }

        public ICommand StartSubmissionCommand { get; }

        public FlightSubmissionViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _contextService = FlightContextService.Instance;
            StatusMessage = "Starting submission...";
            Progress = 0;
            RetryBtnVisible = Visibility.Hidden;
        }

        public async Task SubmitFlightReport()
        {
            StatusMessage = "Starting submission...";
            Progress = 0;
            RetryBtnVisible = Visibility.Hidden;
            // If one step fail just return (each one will set error message)
            List<Func<Task<bool>>> steps = new()
            {
                ExportBlackBox,
                SplitBlackBox,
                SendFlightReport
            };

            int stepProgress = 100 / (steps.Count + 1);

            foreach (var step in steps)
            {
                if (!await step()) return;
                Progress += stepProgress;
            }

            for (int i = 0; i < numberOfChunks; i++)
            {
                if (!await UploadChunk(i, numberOfChunks)) return;
                Progress += stepProgress / numberOfChunks;
            }

            if (await CleanUp())
            {
                Progress = 100;
                OnSubmissionCompleted?.Invoke();
            }
        }

        private async Task<bool> ExportBlackBox()
        {
            StatusMessage = "Exporting events to blackbox file...";
            try
            {
                await _contextService.ExportFlightToJson();
            } catch (Exception ex)
            {
                StatusMessage = $"Error exporting events to blackboxfile: {ex.Message}";
                RetryBtnVisible = Visibility.Visible;
                return false;
            }

            return true;
        }

        private async Task<bool> SplitBlackBox()
        {
            StatusMessage = "Splitting blackbox in pieces...";
            try {
                this.numberOfChunks = await _contextService.SplitBlackBoxData();
            } catch (Exception ex)
            {
                StatusMessage = $"Error splitting blackbox: {ex.Message}";
                RetryBtnVisible = Visibility.Visible;
                return false;
            }
            return true;
        }

        private async Task<bool> SendFlightReport()
        {
            StatusMessage = "Sending basic information...";
            try
            {
                var result = await _contextService.SendFlightReport();
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Error sending basic information (server): {result.ErrorMessage}";
                    RetryBtnVisible = Visibility.Visible;
                    return false;
                }
            } catch (Exception ex)
            {
                StatusMessage = $"Error sending basic information: ${ex.Message}";
                RetryBtnVisible = Visibility.Visible;
                return false;
            }

            return true;
        }

        private async Task<bool> UploadChunk(int i, int totalChunks)
        {
            StatusMessage = $"Uploading black box file {i + 1} of {totalChunks}...";
            try
            {
                var result = await _contextService.UploadChunk(i);
                if (!result.IsSuccess)
                {
                    StatusMessage = $"Error uploading black box file {i + 1} (server): {result.ErrorMessage}";
                    RetryBtnVisible = Visibility.Visible;
                    return false;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error uploading black box file {i + 1} {ex.Message}";
                RetryBtnVisible = Visibility.Visible;
                return false;
            }

            return true;
        }

        private async Task<bool> CleanUp()
        {
            StatusMessage = "Cleaning up...";
            try
            {
                _contextService.CleanPreviousData();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error cleaning up: {ex.Message}";
                RetryBtnVisible = Visibility.Visible;
                return false;
            }
            return true;
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event Action OnSubmissionCompleted;
    }
}
