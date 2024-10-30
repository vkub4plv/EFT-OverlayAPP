using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Tesseract;

namespace EFT_OverlayAPP
{
    public partial class MainWindow : Window
    {
        // First hotkey
        private const int HOTKEY_ID = 9000; // Any number >= 0
        private HwndSource source;

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

        // Hotkey Dll import
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

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
                TimerTextBlock.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(212, 45, 54));
            }
            else
            {
                TimerTextBlock.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(182, 193, 199));
            }
        }

        // Keyboard hotkey
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var helper = new WindowInteropHelper(this);
            source = HwndSource.FromHwnd(helper.Handle);
            source.AddHook(HwndHook);

            RegisterHotKey();
        }

        private void RegisterHotKey()
        {
            var helper = new WindowInteropHelper(this);
            // Modifier keys codes: Alt = 1, Ctrl = 2, Shift = 4, Win = 8
            // Here, we're setting Ctrl + Shift + S as the hotkey
            if (!RegisterHotKey(helper.Handle, HOTKEY_ID, 6, (uint)KeyInterop.VirtualKeyFromKey(Key.S)))
            {
                MessageBox.Show("Failed to register hotkey.");
            }
        }

        private void UnregisterHotKey()
        {
            var helper = new WindowInteropHelper(this);
            UnregisterHotKey(helper.Handle, HOTKEY_ID);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_ID)
                {
                    // Handle hotkey press
                    CaptureAndProcessScreenshot();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        protected override void OnClosed(EventArgs e)
        {
            source.RemoveHook(HwndHook);
            UnregisterHotKey();
            base.OnClosed(e);
        }

        // Capturing a screenshot
        private Bitmap CaptureScreenArea(Rectangle area)
        {
            Bitmap bitmap = new Bitmap(area.Width, area.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(area.Left, area.Top, 0, 0, area.Size, CopyPixelOperation.SourceCopy);
            }
            return bitmap;
        }

        // Usage of the screenshot
        private async void CaptureAndProcessScreenshot()
        {
            try
            {
                // Hide the overlay windows
                this.Hide();
                if (webViewWindow != null)
                {
                    webViewWindow.Hide();
                }

                // Give the system time to refresh the screen without the overlay
                await Task.Delay(100); // Adjust the delay as needed

                // Define the area to capture (top-right corner)
                // For example, capture a rectangle that's 200x100 pixels from the top-right corner
                int width = 200;
                int height = 100;
                int left = (int)(SystemParameters.PrimaryScreenWidth) - width;
                int top = 0;

                Rectangle captureArea = new Rectangle(left, top, width, height);
                Bitmap screenshot = CaptureScreenArea(captureArea);

                // Use preprocessing
                Bitmap preprocessedImage = PreprocessImage(screenshot);

                // Show the overlay windows again
                this.Show();
                if (webViewWindow != null)
                {
                    webViewWindow.Show();
                }

                // Perform OCR asynchronously
                string extractedText = await Task.Run(() => PerformOCR(preprocessedImage));

                // Update the timer based on extracted text
                UpdateTimerFromExtractedText(extractedText);
            }
            catch (Exception ex)
            {
                // Ensure the windows are shown in case of an exception
                this.Show();
                if (webViewWindow != null)
                {
                    webViewWindow.Show();
                }

                MessageBox.Show("Error during screenshot capture: " + ex.Message);
            }
        }

        // Preprocessing
        private Bitmap PreprocessImage(Bitmap image)
        {
            // Convert to grayscale
            Bitmap grayscaleImage = new Bitmap(image.Width, image.Height);
            using (Graphics g = Graphics.FromImage(grayscaleImage))
            {
                ColorMatrix colorMatrix = new ColorMatrix(
                    new float[][]
                    {
                new float[] {0.3f, 0.3f, 0.3f, 0, 0},
                new float[] {0.59f, 0.59f, 0.59f, 0, 0},
                new float[] {0.11f, 0.11f, 0.11f, 0, 0},
                new float[] {0, 0, 0, 1, 0},
                new float[] {0, 0, 0, 0, 1}
                    });
                ImageAttributes attributes = new ImageAttributes();
                attributes.SetColorMatrix(colorMatrix);
                g.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height),
                    0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }
            return grayscaleImage;
        }


        // OCR
        private string PerformOCR(Bitmap image)
        {
            string tessDataPath = @"tessdata"; // Path to your tessdata folder
            string language = "eng"; // Language(s) to use

            try
            {
                using (var engine = new TesseractEngine(tessDataPath, language, EngineMode.Default))
                {
                    // Use PixConverter.ToPix
                    using (var pix = PixConverter.ToPix(image))
                    {
                        using (var page = engine.Process(pix))
                        {
                            string text = page.GetText();
                            return text;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Error performing OCR: " + ex.Message);
                });
                return string.Empty;
            }
        }

        // Parsing the text to obtain time
        private void UpdateTimerFromExtractedText(string extractedText)
        {
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                MessageBox.Show("No text extracted from the screenshot.");
                return;
            }

            // Use a regular expression to find time patterns
            string pattern = @"(\d{1,2}:\d{2}:\d{2})"; // Matches HH:MM:SS or H:MM:SS
            var match = System.Text.RegularExpressions.Regex.Match(extractedText, pattern);

            if (match.Success)
            {
                string timeString = match.Groups[1].Value;
                if (TimeSpan.TryParse(timeString, out TimeSpan extractedTime))
                {
                    // Update your application's timer
                    remainingTime = extractedTime;
                    UpdateTimerText();

                    MessageBox.Show("Timer updated to: " + timeString);
                }
                else
                {
                    MessageBox.Show("Failed to parse time from extracted text.");
                }
            }
            else
            {
                MessageBox.Show("No valid time found in the extracted text.");
            }
        }
    }
}
