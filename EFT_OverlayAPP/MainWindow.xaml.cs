using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
        private WebViewWindow webViewWindow;
        private IntPtr hwnd;

        private DispatcherTimer timer;
        private TimeSpan remainingTime;

        // Hotkey IDs
        private const int HOTKEY_ID_RAID_TIMER = 9001; // Unique ID for Raid Timer OCR hotkey
        private const int HOTKEY_ID_EXFIL_TRANSIT = 9002; // Unique ID for EXFIL/TRANSIT OCR hotkey
        private HwndSource source;

        // Observable collection for data binding
        public ObservableCollection<ExfilTransitEntry> ExfilTransitEntries { get; set; } = new ObservableCollection<ExfilTransitEntry>();

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closed += MainWindow_Closed;

            // Set DataContext for data binding
            this.DataContext = this;

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

            // Register the global hotkeys
            source = HwndSource.FromHwnd(hwnd);
            source.AddHook(HwndHook);
            RegisterHotKeys();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            if (webViewWindow != null)
            {
                webViewWindow.Close();
                webViewWindow = null;
            }

            // Unregister the hotkeys
            source.RemoveHook(HwndHook);
            UnregisterHotKeys();
        }

        // P/Invoke declarations
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        // Hotkey registration P/Invoke
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // Hotkey constants
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const int WM_HOTKEY = 0x0312;

        // Method to register the global hotkeys
        private void RegisterHotKeys()
        {
            // Hotkey for Raid Timer OCR (e.g., Ctrl + Shift + T)
            uint modifiersTimer = MOD_CONTROL | MOD_SHIFT;
            uint virtualKeyTimer = (uint)KeyInterop.VirtualKeyFromKey(Key.T);

            if (!RegisterHotKey(hwnd, HOTKEY_ID_RAID_TIMER, modifiersTimer, virtualKeyTimer))
            {
                MessageBox.Show("Failed to register hotkey for Raid Timer OCR.");
            }

            // Hotkey for EXFIL/TRANSIT OCR (e.g., Ctrl + Shift + E)
            uint modifiersExfil = MOD_CONTROL | MOD_SHIFT;
            uint virtualKeyExfil = (uint)KeyInterop.VirtualKeyFromKey(Key.E);

            if (!RegisterHotKey(hwnd, HOTKEY_ID_EXFIL_TRANSIT, modifiersExfil, virtualKeyExfil))
            {
                MessageBox.Show("Failed to register hotkey for EXFIL/TRANSIT OCR.");
            }
        }

        private void UnregisterHotKeys()
        {
            UnregisterHotKey(hwnd, HOTKEY_ID_RAID_TIMER);
            UnregisterHotKey(hwnd, HOTKEY_ID_EXFIL_TRANSIT);
        }

        // Window message hook to capture hotkey presses
        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_ID_RAID_TIMER)
                {
                    // Handle hotkey press for Raid Timer OCR
                    CaptureAndProcessRaidTimer();
                    handled = true;
                }
                else if (id == HOTKEY_ID_EXFIL_TRANSIT)
                {
                    // Handle hotkey press for EXFIL/TRANSIT OCR
                    CaptureAndProcessExfilTransit();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        // Your existing timer methods
        private void InitializeTimer()
        {
            // Initialize the timer with zero remaining time
            remainingTime = TimeSpan.Zero;

            // Initialize and configure the timer
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1); // Set timer to tick every second
            timer.Tick += Timer_Tick;

            // Start the timer
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // Decrease the remaining time
            if (remainingTime > TimeSpan.Zero)
            {
                remainingTime = remainingTime.Add(TimeSpan.FromSeconds(-1));
            }
            else
            {
                remainingTime = TimeSpan.Zero;
            }
            UpdateTimerText();
        }

        // Update the TextBlock with the remaining time
        private void UpdateTimerText()
        {
            TimerTextBlock.Text = remainingTime.ToString(@"h\:mm\:ss");
            if (remainingTime <= TimeSpan.FromMinutes(10))
            {
                TimerTextBlock.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(212, 45, 54)); // Red color
            }
            else
            {
                TimerTextBlock.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(182, 193, 199)); // Your preferred color
            }
        }

        // Method to capture and process the Raid Timer
        private async void CaptureAndProcessRaidTimer()
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

                // Capture the area with the Raid Timer
                Bitmap raidTimerScreenshot = CaptureRaidTimerArea();

                // Show the overlay windows again
                this.Show();
                if (webViewWindow != null)
                {
                    webViewWindow.Show();
                }

                // Preprocess the image
                Bitmap preprocessedImage = PreprocessImage(raidTimerScreenshot);

                // Perform OCR asynchronously
                string ocrText = await Task.Run(() => PerformOCROnRaidTimer(preprocessedImage));

                // Update the timer based on extracted text
                UpdateRaidTimer(ocrText);
            }
            catch (Exception ex)
            {
                // Ensure the windows are shown in case of an exception
                this.Show();
                if (webViewWindow != null)
                {
                    webViewWindow.Show();
                }

                MessageBox.Show("Error during raid timer OCR: " + ex.Message);
            }
        }

        // Method to capture a portion of the screen for the Raid Timer
        private Bitmap CaptureRaidTimerArea()
        {
            // Define the area to capture (adjust these values)
            int width = 185; // Adjust width as needed
            int height = 70; // Adjust height as needed
            int left = (int)(SystemParameters.VirtualScreenWidth) - width - 7; // Adjust left as needed
            int top = 7; // Adjust top as needed

            System.Drawing.Rectangle captureArea = new System.Drawing.Rectangle(left, top, width, height);
            Bitmap bitmap = new Bitmap(captureArea.Width, captureArea.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(captureArea.Left, captureArea.Top, 0, 0, captureArea.Size, CopyPixelOperation.SourceCopy);
            }
            return bitmap;
        }

        // Method to perform OCR on the Raid Timer
        private string PerformOCROnRaidTimer(Bitmap image)
        {
            string tessDataPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            string language = "eng"; // Language(s) to use

            try
            {
                // Preprocess the image (already done in CaptureAndProcessRaidTimer)
                // Bitmap preprocessedImage = PreprocessImage(image);

                using (var engine = new TesseractEngine(tessDataPath, language, EngineMode.LstmOnly))
                {
                    // Set page segmentation mode
                    engine.DefaultPageSegMode = PageSegMode.SingleLine;

                    // Set whitelist of characters
                    engine.SetVariable("tessedit_char_whitelist", "0123456789:");

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
                    MessageBox.Show("Error performing OCR on raid timer: " + ex.ToString());
                });
                return string.Empty;
            }
        }

        // Update the raid timer based on OCR result
        private void UpdateRaidTimer(string ocrText)
        {
            if (string.IsNullOrWhiteSpace(ocrText))
            {
                MessageBox.Show("No text extracted from the raid timer screenshot.");
                return;
            }

            // Clean up the OCR text
            string timeText = ocrText.Trim().Replace(" ", "").Replace("\n", "").Replace("\r", "");

            // Attempt to parse the time
            if (TimeSpan.TryParseExact(timeText, new[] { @"h\:mm\:ss", @"mm\:ss" }, null, out TimeSpan timeSpan))
            {
                remainingTime = timeSpan;
                Dispatcher.Invoke(() => UpdateTimerText());
            }
            else
            {
                MessageBox.Show("Failed to parse the raid timer from OCR text: " + timeText);
            }
        }

        // Method to capture and process the EXFIL/TRANSIT information
        private async void CaptureAndProcessExfilTransit()
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

                // Capture the area with EXFIL/TRANSIT information
                Bitmap exfilTransitScreenshot = CaptureExfilTransitArea();

                // Show the overlay windows again
                this.Show();
                if (webViewWindow != null)
                {
                    webViewWindow.Show();
                }

                // Preprocess the image
                Bitmap preprocessedImage = PreprocessImage(exfilTransitScreenshot);

                // Perform OCR asynchronously
                string ocrText = await Task.Run(() => PerformOCROnExfilTransit(preprocessedImage));

                // Update the grid based on extracted text
                UpdateExfilTransitGrid(ocrText);
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

        // Method to capture a portion of the screen
        private Bitmap CaptureExfilTransitArea()
        {
            // Define the area to capture (adjust these values)
            int width = 400; // Adjust width as needed
            int height = 600; // Adjust height as needed
            int left = (int)(SystemParameters.VirtualScreenWidth) - width;
            int top = 100; // Adjust top as needed

            System.Drawing.Rectangle captureArea = new System.Drawing.Rectangle(left, top, width, height);
            Bitmap bitmap = new Bitmap(captureArea.Width, captureArea.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(captureArea.Left, captureArea.Top, 0, 0, captureArea.Size, CopyPixelOperation.SourceCopy);
            }
            return bitmap;
        }

        // Method to perform OCR on the EXFIL/TRANSIT information
        private string PerformOCROnExfilTransit(Bitmap image)
        {
            string tessDataPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            string language = "eng"; // Language(s) to use

            try
            {
                // Preprocess the image (already done in CaptureAndProcessExfilTransit)
                // Bitmap preprocessedImage = PreprocessImage(image);

                using (var engine = new TesseractEngine(tessDataPath, language, EngineMode.LstmOnly))
                {
                    // Set page segmentation mode
                    engine.DefaultPageSegMode = PageSegMode.SingleBlock;

                    // Set whitelist of characters
                    engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789:?- ");

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
                    MessageBox.Show("Error performing OCR: " + ex.ToString());
                });
                return string.Empty;
            }
        }

        // Preprocess the image before OCR
        private Bitmap PreprocessImage(Bitmap image)
        {
            // Convert to grayscale
            Bitmap grayImage = new Bitmap(image.Width, image.Height);
            using (Graphics g = Graphics.FromImage(grayImage))
            {
                var colorMatrix = new ColorMatrix(
                    new float[][]
                    {
                        new float[] {0.299f, 0.299f, 0.299f, 0, 0},
                        new float[] {0.587f, 0.587f, 0.587f, 0, 0},
                        new float[] {0.114f, 0.114f, 0.114f, 0, 0},
                        new float[] {0,      0,      0,      1, 0},
                        new float[] {0,      0,      0,      0, 1}
                    });
                var attributes = new ImageAttributes();
                attributes.SetColorMatrix(colorMatrix);
                g.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height),
                    0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }

            // Apply adaptive thresholding
            Bitmap binaryImage = ApplyThreshold(grayImage);

            // Optionally, resize the image to improve OCR accuracy
            Bitmap resizedImage = new Bitmap(binaryImage, new System.Drawing.Size(binaryImage.Width * 2, binaryImage.Height * 2));

            return resizedImage;
        }

        // Apply thresholding to the image
        private Bitmap ApplyThreshold(Bitmap image)
        {
            Bitmap result = new Bitmap(image.Width, image.Height);

            // Simple threshold
            int threshold = 128; // Adjust as needed

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    System.Drawing.Color pixel = image.GetPixel(x, y);
                    int intensity = pixel.R; // Since the image is grayscale, R=G=B

                    if (intensity < threshold)
                    {
                        result.SetPixel(x, y, System.Drawing.Color.Black);
                    }
                    else
                    {
                        result.SetPixel(x, y, System.Drawing.Color.White);
                    }
                }
            }

            return result;
        }

        // Update the grid with the parsed entries
        private void UpdateExfilTransitGrid(string ocrText)
        {
            if (string.IsNullOrWhiteSpace(ocrText))
            {
                MessageBox.Show("No text extracted from the screenshot.");
                return;
            }

            // Split the OCR text into lines
            string[] lines = ocrText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Create a list to hold the parsed entries
            var entries = new List<ExfilTransitEntry>();

            foreach (var line in lines)
            {
                string pattern = @"^(EXFIL|TRANSIT)(\d+)\s+(.+?)\s+(\d{1,2}:\d{2}:\d{2}|[\?]{2}:[\?]{2}:[\?]{2}|)$";
                var match = System.Text.RegularExpressions.Regex.Match(line, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    string type = match.Groups[1].Value.ToUpper();
                    int index = int.Parse(match.Groups[2].Value);
                    string name = match.Groups[3].Value.Trim();
                    string timeString = match.Groups[4].Value.Trim();

                    var entry = new ExfilTransitEntry
                    {
                        Type = type,
                        Index = index,
                        Name = name,
                        TimeStringRaw = timeString
                    };

                    // For entries with a valid time, initialize the timer
                    if (TimeSpan.TryParse(timeString, out TimeSpan timeSpan))
                    {
                        entry.RemainingTime = timeSpan;
                        StartEntryTimer(entry);
                    }

                    entries.Add(entry);
                }
                else
                {
                    // Handle lines that don't match the pattern
                }
            }

            // Update the grid with the parsed entries
            UpdateGrid(entries);
        }

        private void UpdateGrid(List<ExfilTransitEntry> entries)
        {
            // Clear the existing entries
            ExfilTransitEntries.Clear();

            // Add new entries
            foreach (var entry in entries)
            {
                ExfilTransitEntries.Add(entry);
            }
        }

        // Start the timer for an entry (both EXFIL and TRANSIT)
        private void StartEntryTimer(ExfilTransitEntry entry)
        {
            DispatcherTimer entryTimer = new DispatcherTimer();
            entryTimer.Interval = TimeSpan.FromSeconds(1);
            entryTimer.Tick += (s, e) =>
            {
                if (entry.RemainingTime.HasValue && entry.RemainingTime.Value > TimeSpan.Zero)
                {
                    entry.RemainingTime = entry.RemainingTime.Value.Add(TimeSpan.FromSeconds(-1));
                }
                else
                {
                    // Time's up
                    entryTimer.Stop();
                    entry.RemainingTime = TimeSpan.Zero;
                }
            };
            entryTimer.Start();
        }
    }

    // ExfilTransitEntry class
    public class ExfilTransitEntry : INotifyPropertyChanged
    {
        public string Type { get; set; } // "EXFIL" or "TRANSIT"
        public int Index { get; set; } // EXFIL/TRANSIT number (e.g., 01)
        public string Name { get; set; }

        private TimeSpan? remainingTime;
        public TimeSpan? RemainingTime
        {
            get { return remainingTime; }
            set
            {
                remainingTime = value;
                OnPropertyChanged(nameof(RemainingTime));
                OnPropertyChanged(nameof(TimeString));
                OnPropertyChanged(nameof(Color));
            }
        }

        public string TimeStringRaw { get; set; } // Original time string from OCR

        public string TimeString
        {
            get
            {
                if (RemainingTime.HasValue)
                {
                    if (RemainingTime.Value > TimeSpan.Zero)
                    {
                        return RemainingTime.Value.ToString(@"h\:mm\:ss");
                    }
                    else
                    {
                        return string.Empty; // Time's up, display blank
                    }
                }
                else
                {
                    return TimeStringRaw;
                }
            }
        }

        public SolidColorBrush Color
        {
            get
            {
                if (RemainingTime.HasValue)
                {
                    if (RemainingTime.Value > TimeSpan.Zero)
                    {
                        if (Type == "TRANSIT")
                        {
                            // Active TRANSIT timer: Red
                            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(186, 12, 7));
                        }
                        else if (Type == "EXFIL")
                        {
                            // Active EXFIL timer: Green
                            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 210, 2));
                        }
                        else
                        {
                            // Default color for unknown type
                            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0, 0));
                        }
                    }
                    else
                    {
                        // Timer has run out
                        if (Type == "TRANSIT")
                        {
                            // TRANSIT timer ran out: Orange
                            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(242, 117, 46));
                        }
                        else if (Type == "EXFIL")
                        {
                            // EXFIL timer ran out: Red
                            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(186, 12, 7));
                        }
                        else
                        {
                            // Default color for unknown type
                            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0, 0));
                        }
                    }
                }
                else
                {
                    // No timer
                    if (Type == "TRANSIT")
                    {
                        // TRANSIT without timer: Orange
                        return new SolidColorBrush(System.Windows.Media.Color.FromRgb(242, 117, 46));
                    }
                    else if (Type == "EXFIL")
                    {
                        // EXFIL without timer: Default color
                        return new SolidColorBrush(System.Windows.Media.Color.FromRgb(182, 193, 199));
                    }
                    else
                    {
                        // Default color for unknown type
                        return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0, 0));
                    }
                }
            }
        }

        // Implement INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
