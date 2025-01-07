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
            mainWindow.UpdateCanvases();
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

        public void UpdateButtonsCanvas(double BaseWidth, double BaseHeight, double targetWidth, double scaleFactorX, double scaleFactorY)
        {
            this.Width = ButtonsCanvas.Width;
            this.Height = ButtonsCanvas.Height;
            this.Top = 0;
            this.Left = ((mainWindow.ActualWidth - ButtonsCanvas.Width) / 2);

            foreach (var child in ButtonsCanvas.Children)
            {
                if (child is FrameworkElement element)
                {
                    if (element.Name.Equals("OpenCraftingWindowButton"))
                    {
                        OpenCraftingWindowButton.RenderTransform = new ScaleTransform(scaleFactorX, scaleFactorX);
                        OpenCraftingWindowButton.RenderTransformOrigin = new System.Windows.Point(0.5, 0);

                        Canvas.SetLeft(OpenCraftingWindowButton, (1670 * scaleFactorX));
                        Canvas.SetTop(OpenCraftingWindowButton, (15 * scaleFactorY));
                    }

                    if (element.Name.Equals("OpenRequiredItemsWindowButton"))
                    {
                        OpenRequiredItemsWindowButton.RenderTransform = new ScaleTransform(scaleFactorX, scaleFactorX);
                        OpenRequiredItemsWindowButton.RenderTransformOrigin = new System.Windows.Point(0.5, 0);

                        Canvas.SetLeft(OpenRequiredItemsWindowButton, ((1670 + OpenCraftingWindowButton.ActualWidth + 5) * scaleFactorX));
                        Canvas.SetTop(OpenRequiredItemsWindowButton, (15 * scaleFactorY));
                    }

                    if (element.Name.Equals("OpenConfigWindowButton"))
                    {
                        OpenConfigWindowButton.RenderTransform = new ScaleTransform(scaleFactorX, scaleFactorX);
                        OpenConfigWindowButton.RenderTransformOrigin = new System.Windows.Point(0.5, 0);

                        Canvas.SetLeft(OpenConfigWindowButton, ((1670 + OpenCraftingWindowButton.ActualWidth + OpenRequiredItemsWindowButton.ActualWidth + 10) * scaleFactorX));
                        Canvas.SetTop(OpenConfigWindowButton, (15 * scaleFactorY));
                    }
                }
            }
        }
    }
}
