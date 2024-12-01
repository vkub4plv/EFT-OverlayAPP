using NLog;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;

namespace EFT_OverlayAPP
{
    public partial class WebViewWindow : Window
    {
        private GameState gameState;
        private double originalTop; // To store the original Top position
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

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

            // Store the original Top position
            originalTop = this.Top;

            // Set DataContext to GameState for data binding if needed
            this.DataContext = gameState;

            // Subscribe to PropertyChanged event to handle changes in OverlayUrl
            this.gameState.PropertyChanged += GameState_PropertyChanged;
        }

        private async void WebViewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await BrowserControl.EnsureCoreWebView2Async(null);

            // Set initial Source if necessary
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
                    else
                    {
                        BrowserControl.Source = new Uri("https://mapgenie.io/tarkov");
                    }
                });
            }
            else if (e.PropertyName == nameof(GameState.IsInRaid))
            {
                Dispatcher.Invoke(() =>
                {
                    if (gameState.IsInRaid)
                    {
                        MoveWindowDown();
                    }
                    else
                    {
                        ResetWindowPosition();
                    }
                });
            }
        }

        private void MoveWindowDown()
        {
            DoubleAnimation animation = new DoubleAnimation
            {
                To = originalTop + 325,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            this.BeginAnimation(Window.TopProperty, animation);
            logger.Info($"WebViewWindow moved down to Top={originalTop + 300}");
        }

        private void ResetWindowPosition()
        {
            DoubleAnimation animation = new DoubleAnimation
            {
                To = originalTop,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            this.BeginAnimation(Window.TopProperty, animation);
            logger.Info($"WebViewWindow reset to original Top={originalTop}");
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            BrowserControl.Width = this.ActualWidth;
            BrowserControl.Height = this.ActualHeight;
        }
    }
}