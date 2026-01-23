using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Velopack;

namespace MamAcars
{
    public partial class App : Application
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Velopack debe ejecutarse ANTES de cualquier otra cosa
            VelopackApp.Build().Run();

            var app = new App();
            app.InitializeComponent();
            app.MainWindow = new MainWindow();
            app.MainWindow.Show();
            app.Run();
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

            try
            {
                // TODO: Add authentication to update requests using user's Bearer token
                // to prevent non-logged users from downloading updates
                var mgr = new UpdateManager(BrandingConfig.UpdateUrl);
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
