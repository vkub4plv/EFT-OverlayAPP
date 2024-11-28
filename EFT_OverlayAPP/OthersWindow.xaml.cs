using System;
using System.Collections.Generic;
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

        public OthersWindow(MainWindow mainWindow, GameState gameState)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            this.gameState = gameState;
            this.DataContext = gameState; // Set DataContext
            this.Owner = mainWindow;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Position the window at the specified coordinates
            this.Left = 1670;
            this.Top = 15;

            // Adjust for DPI scaling
            AdjustForDpi();

            // Set the window size to fit the content
            this.Width = (OpenCraftingWindowButton.Width + OpenRequiredItemsWindowButton.Width + 5); // 5 is the margin between buttons
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
    }
}
