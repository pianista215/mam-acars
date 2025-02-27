using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace MamAcars
{
    public partial class App : Application
    {
        public App()
        {
            string logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MamAcars",
                "logs"
            );

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

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("App closed");
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
