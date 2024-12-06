using NLog;
using System.Configuration;
using System.Data;
using System.Windows;

namespace EFT_OverlayAPP
{
    public partial class App : Application
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        public static bool IsPVEMode { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            logger.Info("Application starting up.");

            // Show the main window
            MainWindow mainWindow = new MainWindow();
            MainWindow = mainWindow; // Set the main window
            mainWindow.Show();
        }
    }
}
