using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace EFT_OverlayAPP
{
    public partial class OthersWindow : Window
    {
        private MainWindow mainWindow;
        private GameState gameState;
        private ConfigWindow configWindow;

        public OthersWindow(MainWindow mainWindow, GameState gameState, ConfigWindow configWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            this.gameState = gameState;
            this.configWindow = configWindow;
            var dataBinding = new OthersWindowDataBinding
            {
                GameState = gameState,
                Config = configWindow.AppConfig,
                Main = mainWindow,
                IsInRaid = gameState.IsInRaid,
                HideOtherWindowButtonsWhenInRaid = configWindow.AppConfig.HideOtherWindowButtonsWhenInRaid,
                ManualOtherWindowButtonsVisibilityOverride = mainWindow.ManualOtherWindowButtonsVisibilityOverride,
                IsRequiredDataLoading = mainWindow.requiredItemsWindow.IsRequiredDataLoading,
                IsLoading = mainWindow.craftingWindow.IsLoading
            };
            this.DataContext = dataBinding; // Set DataContext
            this.Owner = mainWindow;

            gameState.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(gameState.IsInRaid))
                {
                    dataBinding.IsInRaid = gameState.IsInRaid;
                }
            };

            mainWindow.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(mainWindow.ManualOtherWindowButtonsVisibilityOverride))
                {
                    dataBinding.ManualOtherWindowButtonsVisibilityOverride = mainWindow.ManualOtherWindowButtonsVisibilityOverride;
                }
            };

            mainWindow.requiredItemsWindow.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(mainWindow.requiredItemsWindow.IsRequiredDataLoading))
                {
                    dataBinding.IsRequiredDataLoading = mainWindow.requiredItemsWindow.IsRequiredDataLoading;
                }
            };

            mainWindow.craftingWindow.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(mainWindow.craftingWindow.IsLoading))
                {
                    dataBinding.IsLoading = mainWindow.craftingWindow.IsLoading;
                }
            };

            configWindow.AppConfig.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(configWindow.AppConfig.HideOtherWindowButtonsWhenInRaid))
                {
                    dataBinding.HideOtherWindowButtonsWhenInRaid = configWindow.AppConfig.HideOtherWindowButtonsWhenInRaid;
                }
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Position the window at the specified coordinates
            this.Left = 1670;
            this.Top = 15;

            // Adjust for DPI scaling
            AdjustForDpi();

            // Set the window size to fit the content
            this.Width = (OpenCraftingWindowButton.Width + OpenRequiredItemsWindowButton.Width + OpenConfigWindowButton.Width + 10); // 5 * 2 = 10 is the margin between buttons
            this.Height = OpenCraftingWindowButton.Height;
        }

        private void AdjustForDpi()
        {
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source != null)
            {
                double dpiX = source.CompositionTarget.TransformToDevice.M11;
                double dpiY = source.CompositionTarget.TransformToDevice.M22;

                this.Left *= dpiX;
                this.Top *= dpiY;

                this.Width *= dpiX;
                this.Height *= dpiY;
            }
        }

        private void OpenCraftingWindowButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.OpenCraftingWindow();
        }

        private void OpenRequiredItemsWindowButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.OpenRequiredItemsWindow();
        }

        private void OpenConfigWindowButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.OpenConfigWindow();
        }
    }
}
