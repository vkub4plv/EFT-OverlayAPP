using System;
using System.Windows;

namespace EFT_OverlayAPP
{
    public partial class WebViewWindow : Window
    {
        public WebViewWindow(Window owner)
        {
            InitializeComponent();
            this.Loaded += WebViewWindow_Loaded;

            // Set the owner
            this.Owner = owner;

            // Calculate position relative to owner
            var ownerPosition = owner.PointToScreen(new Point(0, 0));
            this.Left = ownerPosition.X;
            this.Top = ownerPosition.Y;
            this.Width = 480;
            this.Height = 270;
        }


        private async void WebViewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await BrowserControl.EnsureCoreWebView2Async(null);
            BrowserControl.Source = new Uri("https://mapgenie.io/tarkov/maps/customs");

            // Ensure the WebView2 control fills the window
            BrowserControl.Width = this.ActualWidth;
            BrowserControl.Height = this.ActualHeight;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            BrowserControl.Width = this.ActualWidth;
            BrowserControl.Height = this.ActualHeight;
        }
    }
}
