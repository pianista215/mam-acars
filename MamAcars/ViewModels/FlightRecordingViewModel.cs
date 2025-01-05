using MamAcars.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MamAcars.ViewModels
{
    public class FlightRecordingViewModel : INotifyPropertyChanged
    {
        private DispatcherTimer _timer;
        private DateTime _startTime;
        private Stopwatch stopWatch;
        private string _elapsedTime = "00:00:00";

        private readonly FsuipcService _fsuipcService;

        public event PropertyChangedEventHandler PropertyChanged;

        public string ElapsedTime
        {
            get => _elapsedTime;
            set
            {
                _elapsedTime = value;
                OnPropertyChanged("ElapsedTime");
            }
        }
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public FlightRecordingViewModel()
        {
            _startTime = DateTime.Now;
            stopWatch = new Stopwatch();
            _timer = new DispatcherTimer();
            _timer.Tick += OnDispatcherTimerTick;
            _timer.Interval = new TimeSpan(0, 0, 0, 0, 1);
            stopWatch.Start();
            _timer.Start();
            _fsuipcService = FsuipcService.Instance;
            _fsuipcService.startSavingBlackBox();
        }

        private void OnDispatcherTimerTick(object sender, EventArgs e)
        {
            ElapsedTime = stopWatch.Elapsed.ToString(@"hh\:mm\:ss");
            PropertyChanged(this, new PropertyChangedEventArgs("ElapsedTime"));
        }

        public void StopTimer()
        {
            _timer.Stop();
        }
    }
}
