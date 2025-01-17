using NLog;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EFT_OverlayAPP
{
    public partial class WebViewWindow : Window
    {
        private GameState gameState;
        private ConfigWindow configWindow;
        private GameStateManager gameStateManager;
        private MainWindow mainWindow;
        private double originalTop; // To store the original Top position
        private double originalLeft; // To store the original Left position
        private double originalWidth; // Original Width
        private double originalHeight; // Original Height
        private double wvTargetWidth = 0;
        private double wvTargetHeight = 0;
        private double wvNewLeft = 0;
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private DebounceDispatcher debounceDispatcher = new DebounceDispatcher(100); // 0.1 second debounce

        public WebViewWindow(MainWindow mainWindow, ConfigWindow configWindow, GameStateManager gameStateManager)
        {
            InitializeComponent();
            this.gameState = gameStateManager.GameState;
            this.configWindow = configWindow;
            this.gameStateManager = gameStateManager;
            this.Loaded += WebViewWindow_Loaded;
            this.mainWindow = mainWindow;

            // Set the owner
            this.Owner = mainWindow;

            // Calculate position relative to owner
            var ownerPosition = mainWindow.PointToScreen(new Point(0, 0));
            this.Left = ownerPosition.X;
            this.Top = ownerPosition.Y;
            this.Width = 720;
            this.Height = 405;

            // Store the original Top, Left, Width, and Height positions
            originalTop = this.Top;
            originalLeft = this.Left;
            originalWidth = this.Width;
            originalHeight = this.Height;

            // Set DataContext to GameState for data binding if needed
            this.DataContext = gameState;

            // Subscribe to property changes
            this.configWindow.AppConfig.PropertyChanged += ConfigWindow_PropertyChanged;

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
                BrowserControl.Source = new Uri(gameStateManager.GetDefaultMapUrl());
            }

            // Ensure the WebView2 control fills the window
            BrowserControl.Width = this.ActualWidth;
            BrowserControl.Height = this.ActualHeight;

            // Set zoom level if needed
            BrowserControl.CoreWebView2.Settings.IsZoomControlEnabled = false;
            BrowserControl.ZoomFactor = 0.8; // Adjust as necessary

            mainWindow.UpdateCanvases();
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
                        BrowserControl.Source = new Uri(gameStateManager.GetDefaultMapUrl());
                    }
                });
            }
            else if (e.PropertyName == nameof(GameState.IsInRaid) || e.PropertyName == nameof(GameState.IsMatching))
            {
                Dispatcher.Invoke(() =>
                {
                    gameStateManager.UpdateOverlayUrl();

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

        private void ConfigWindow_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(configWindow.AppConfig.SelectedMapWebsite))
            {
                gameStateManager.UpdateOverlayUrl();
            }
        }


        private void MoveWindowDown()
        {
            return;
            debounceDispatcher.Debounce(() => mainWindow.UpdateCanvases());

            double targetWidth = wvTargetWidth;
            double targetHeight = wvTargetHeight;
            double newTop = mainWindow.ActualHeight - targetHeight;
            double newLeft = wvNewLeft;

            // Animate Width
            DoubleAnimation widthAnimation = new DoubleAnimation
            {
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            this.BeginAnimation(Window.WidthProperty, widthAnimation);

            // Animate Height
            DoubleAnimation heightAnimation = new DoubleAnimation
            {
                To = targetHeight,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            this.BeginAnimation(Window.HeightProperty, heightAnimation);

            // Animate the Top property
            DoubleAnimation topAnimation = new DoubleAnimation
            {
                To = newTop,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            this.BeginAnimation(Window.TopProperty, topAnimation);

            // Animate the Left property
            DoubleAnimation leftAnimation = new DoubleAnimation
            {
                To = newLeft,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            this.BeginAnimation(Window.LeftProperty, leftAnimation);

            logger.Info($"WebViewWindow resized to {targetWidth}x{targetHeight} and moved to Top={newTop}, Left={newLeft}");
        }

        private void ResetWindowPosition()
        {
            return;
            // Animate Width back to original
            DoubleAnimation widthAnimation = new DoubleAnimation
            {
                To = originalWidth,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            this.BeginAnimation(Window.WidthProperty, widthAnimation);

            // Animate Height back to original
            DoubleAnimation heightAnimation = new DoubleAnimation
            {
                To = originalHeight,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            this.BeginAnimation(Window.HeightProperty, heightAnimation);

            // Animate the Top property back to original
            DoubleAnimation topAnimation = new DoubleAnimation
            {
                To = originalTop,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            this.BeginAnimation(Window.TopProperty, topAnimation);

            // Animate the Left property back to original
            DoubleAnimation leftAnimation = new DoubleAnimation
            {
                To = originalLeft,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            this.BeginAnimation(Window.LeftProperty, leftAnimation);

            logger.Info($"WebViewWindow reset to original size {originalWidth}x{originalHeight} and position Top={originalTop}, Left={originalLeft}");
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            BrowserControl.Width = this.ActualWidth;
            BrowserControl.Height = this.ActualHeight;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (configWindow?.AppConfig != null)
                configWindow.AppConfig.PropertyChanged -= ConfigWindow_PropertyChanged;

            if (gameState != null)
                gameState.PropertyChanged -= GameState_PropertyChanged;
        }

        public void UpdateMinimapCanvas(double BaseWidth, double BaseHeight, double targetWidth, double scaleFactorX, double scaleFactorY)
        {
            this.Width = 720 * scaleFactorX;
            this.Height = 405 * scaleFactorX;
            originalWidth = 720 * scaleFactorX;
            originalHeight = 405 * scaleFactorX;
            BrowserControl.Width = this.ActualWidth;
            BrowserControl.Height = this.ActualHeight;
            wvTargetWidth = 470 * scaleFactorX;
            wvTargetHeight = 200 * scaleFactorX;
            wvNewLeft = 275 * scaleFactorX;
        }
    }
}