using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace EFT_OverlayAPP
{
    public partial class MainWindow : Window
    {
        private WebViewWindow webViewWindow;
        private IntPtr hwnd;

        private DispatcherTimer timer;
        private TimeSpan remainingTime;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closed += MainWindow_Closed;

            // Initialize your existing timer or other overlay content here
            InitializeTimer();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Get the window handle
            hwnd = new WindowInteropHelper(this).Handle;

            // Make the window click-through
            int exStyle = (int)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)(exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT));

            // Create and show the WebView window
            webViewWindow = new WebViewWindow(this);
            webViewWindow.Show();

            // If you removed the PositionWebViewWindow method, you can comment out this line
            // PositionWebViewWindow();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            if (webViewWindow != null)
            {
                webViewWindow.Close();
                webViewWindow = null;
            }
        }

        // You can remove or comment out the PositionWebViewWindow method and any overrides that call it

        // P/Invoke declarations
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        // Your existing timer methods
        private void InitializeTimer()
        {
            // Set the countdown duration (e.g., 50 minutes)
            remainingTime = TimeSpan.FromMinutes(50);

            // Initialize and configure the timer
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1); // Set timer to tick every second
            timer.Tick += Timer_Tick;

            // Start the timer
            UpdateTimerText();
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // Decrease the remaining time
            if (remainingTime > TimeSpan.Zero)
            {
                remainingTime = remainingTime.Add(TimeSpan.FromSeconds(-1));
                UpdateTimerText();
            }
            else
            {
                // Time's up! Stop the timer
                timer.Stop();
                TimerTextBlock.Text = "Time's up!";
            }
        }

        // Update the TextBlock with the remaining time
        private void UpdateTimerText()
        {
            TimerTextBlock.Text = remainingTime.ToString(@"h\:mm\:ss");
            if (remainingTime <= TimeSpan.FromMinutes(10))
            {
                TimerTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(212, 45, 54));
            }
            else
            {
                TimerTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            }
        }
    }
}
