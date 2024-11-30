using System.Configuration;
using System.Data;
using System.Windows;

namespace EFT_OverlayAPP
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Start data loading
            Task.Run(() => DataCache.LoadDataAsync());

            // Show the main window
            MainWindow mainWindow = new MainWindow();
            MainWindow = mainWindow; // Set the main window
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            // Save crafts data
            var activeCrafts = DataCache.CraftableItems
                .Where(c => c.CraftStatus != CraftStatus.NotStarted)
                .ToList();

            CraftingDataManager.SaveCraftsData(activeCrafts);
        }
    }
}
