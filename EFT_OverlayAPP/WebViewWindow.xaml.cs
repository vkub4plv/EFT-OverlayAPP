using System;
using System.ComponentModel;
using System.Windows;

namespace EFT_OverlayAPP
{
    public partial class WebViewWindow : Window
    {
        private GameState gameState;

        public WebViewWindow(Window owner, GameState gameState)
        {
            InitializeComponent();
            this.gameState = gameState;
            this.Loaded += WebViewWindow_Loaded;

            // Set the owner
            this.Owner = owner;

            // Calculate position relative to owner
            var ownerPosition = owner.PointToScreen(new Point(0, 0));
            this.Left = ownerPosition.X;
            this.Top = ownerPosition.Y;
            this.Width = 480;
            this.Height = 270;

            // Set DataContext to GameState for data binding if needed
            this.DataContext = gameState;

            // Subscribe to PropertyChanged event to handle changes in OverlayUrl
            this.gameState.PropertyChanged += GameState_PropertyChanged;
        }

        private async void WebViewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await BrowserControl.EnsureCoreWebView2Async(null);

            // Set initial Source if necessary
            // Set initial Source
            if (!string.IsNullOrEmpty(gameState.OverlayUrl))
            {
                BrowserControl.Source = new Uri(gameState.OverlayUrl);
            }
            else
            {
                // Set to default URL if OverlayUrl is null or empty
                BrowserControl.Source = new Uri("https://mapgenie.io/tarkov");
            }

            // Ensure the WebView2 control fills the window
            BrowserControl.Width = this.ActualWidth;
            BrowserControl.Height = this.ActualHeight;
        }

        private void GameState_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GameState.OverlayUrl))
            {
                Dispatcher.Invoke(() =>
                {
                    if (!string.IsNullOrEmpty(gameState.OverlayUrl))
                    {
                        BrowserControl.Source = new Uri(gameState.OverlayUrl);
                    }
                });
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            BrowserControl.Width = this.ActualWidth;
            BrowserControl.Height = this.ActualHeight;
        }
    }
}