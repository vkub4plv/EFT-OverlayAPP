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

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            logger.Info("Application starting up.");

            try
            {
                // Start all tasks
                var craftableItemsTask = TarkovApiService.GetCraftableItemsDataAsync();
                var craftModuleSettingsTask = TarkovApiService.GetCraftModuleSettingsDataAsync();
                var requiredItemsTask = TarkovApiService.GetRequiredItemsDataAsync();

                // Wait for all tasks to complete
                await Task.WhenAll(craftableItemsTask, craftModuleSettingsTask, requiredItemsTask);

                logger.Info("All API data has been successfully loaded.");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while loading data from Tarkov.dev API.");
                MessageBox.Show("Failed to load application data. Check logs for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Show the main window
            MainWindow mainWindow = new MainWindow();
            MainWindow = mainWindow; // Set the main window
            mainWindow.Show();
        }
    }
}
