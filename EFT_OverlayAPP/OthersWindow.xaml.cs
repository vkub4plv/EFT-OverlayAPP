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

        public OthersWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            this.Owner = mainWindow;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Position the window at the specified coordinates
            this.Left = 1670;
            this.Top = 15;

            // Adjust for DPI scaling
            AdjustForDpi();

            // Set the window size to fit the button (already set in XAML)
        }

        private void OpenCraftingWindowButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.OpenCraftingWindow();
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
            }
        }
    }
}
