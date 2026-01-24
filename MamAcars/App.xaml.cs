using MamAcars.Services;
using Serilog;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Velopack;
using Velopack.Sources;

namespace MamAcars
{
    public partial class App : Application
    {
        private static Mutex? _mutex;

        [STAThread]
        public static void Main(string[] args)
        {
            // Velopack must be executed before anything
            // Disable auto-apply on startup - we handle updates manually in CheckForUpdates()
            VelopackApp.Build()
                .SetAutoApplyOnStartup(false)
                .Run();

            _mutex = new Mutex(true, $"Global\\{BrandingConfig.AppId}_SingleInstance", out bool createdNew);

            if (!createdNew)
            {
                MessageBox.Show(
                    "La aplicación ya está en ejecución.",
                    BrandingConfig.AppName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                var app = new App();
                app.InitializeComponent();
                app.MainWindow = new MainWindow();
                app.MainWindow.Show();
                app.Run();
            }
            finally
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
        }

        public App()
        {
            string logDirectory = Path.Combine(BrandingConfig.DataDirectory, "logs");

            Directory.CreateDirectory(logDirectory);

            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(Path.Combine(logDirectory, "app.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 5,
                    fileSizeLimitBytes: 10_000_000,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("App started");
        }

        public static async Task CheckForUpdates()
        {
            if (string.IsNullOrEmpty(BrandingConfig.UpdateUrl))
                return;

            var token = TokenStorage.GetToken();
            if (string.IsNullOrEmpty(token))
                return;

            try
            {
                var downloader = new AuthenticatedFileDownloader(token);
                var source = new SimpleWebSource(BrandingConfig.UpdateUrl, downloader);
                var mgr = new UpdateManager(source);

                var newVersion = await mgr.CheckForUpdatesAsync();

                if (newVersion == null)
                    return;

                Log.Information("New version available: {Version}. Downloading...", newVersion.TargetFullRelease.Version);

                await mgr.DownloadUpdatesAsync(newVersion);

                Log.Information("Update downloaded. Restarting...");
                mgr.ApplyUpdatesAndRestart(newVersion);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to check for updates");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("App closed");
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
