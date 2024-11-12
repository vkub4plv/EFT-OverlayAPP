using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Tesseract;
using AForge.Imaging.Filters;
using System.Text.RegularExpressions;

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
                await Task.Delay(500); // Adjust the delay as needed

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

                // Save images for debugging
                raidTimerScreenshot.Save("raid_timer_screenshot.png", System.Drawing.Imaging.ImageFormat.Png);
                preprocessedImage.Save("preprocessed_raid_timer.png", System.Drawing.Imaging.ImageFormat.Png);

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

        private double GetDpiScaleFactor()
        {
            PresentationSource source = PresentationSource.FromVisual(Application.Current.MainWindow);
            double dpiX = 1.0, dpiY = 1.0;
            if (source != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }
            return dpiX; // Assuming uniform scaling
        }

        // Method to capture a portion of the screen for the Raid Timer
        private Bitmap CaptureRaidTimerArea()
        {
            double dpiScale = GetDpiScaleFactor();

            // Define the area to capture (adjust these values)
            int width = (int)(185 * dpiScale); // Adjust width as needed
            int height = (int)(70 * dpiScale); // Adjust height as needed
            int left = (int)(2370 * dpiScale); // Adjust left as needed
            int top = (int)(7 * dpiScale); // Adjust top as needed

            Rectangle captureArea = new Rectangle(left, top, width, height);
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
            string tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            string language = "eng+Bender+Bender-Bold"; // Language(s) to use

            try
            {
                using (var engine = new TesseractEngine(tessDataPath, language, EngineMode.Default))
                {
                    // Set page segmentation mode
                    engine.DefaultPageSegMode = PageSegMode.SingleLine;

                    // Set whitelist of characters
                    engine.SetVariable("tessedit_char_whitelist", "0123456789:");

                    // Disable dictionary to prevent word correction
                    engine.SetVariable("load_system_dawg", "F");
                    engine.SetVariable("load_freq_dawg", "F");

                    using (var pix = PixConverter.ToPix(image))
                    {
                        using (var page = engine.Process(pix))
                        {
                            string text = page.GetText();

                            // Display the extracted OCR text for debugging
                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show("Extracted OCR Text:\n" + text);
                            });

                            return text;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Error performing OCR on raid timer: " + ex.Message);
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
            ocrText = CleanOcrText(ocrText);

            // Use a regular expression to find time patterns
            string pattern = @"(\d{1,2}:\d{2}:\d{2})"; // Matches HH:MM:SS or H:MM:SS
            var match = Regex.Match(ocrText, pattern);

            if (match.Success)
            {
                string timeString = match.Groups[1].Value;
                if (TimeSpan.TryParse(timeString, out TimeSpan extractedTime))
                {
                    // Update your application's timer
                    remainingTime = extractedTime;
                    Dispatcher.Invoke(() => UpdateTimerText());
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
                await Task.Delay(500); // Adjust the delay as needed

                // Capture the area with EXFIL/TRANSIT information
                Bitmap exfilTransitScreenshot = CaptureExfilTransitArea();

                // Show the overlay windows again
                this.Show();
                if (webViewWindow != null)
                {
                    webViewWindow.Show();
                }

                // Save the original screenshot for debugging
                exfilTransitScreenshot.Save("exfil_transit_screenshot.png", System.Drawing.Imaging.ImageFormat.Png);

                // Preprocess the image for word detection
                Bitmap preprocessedImage = PreprocessImage(exfilTransitScreenshot);

                // Locate EXFIL and TRANSIT words
                List<Rectangle> wordRectangles = LocateExfilTransitWords(preprocessedImage);

                if (wordRectangles.Count == 0)
                {
                    MessageBox.Show("No EXFIL or TRANSIT words found.");
                    return;
                }

                // Get entry rectangles based on word positions
                List<Rectangle> entryRectangles = GetEntryRectangles(wordRectangles, exfilTransitScreenshot.Width);

                // Extract and process each entry
                List<string> entryTexts = ExtractEntryTexts(exfilTransitScreenshot, entryRectangles);

                // Update the grid based on extracted texts
                UpdateExfilTransitGrid(entryTexts);
            }
            catch (Exception ex)
            {
                // Ensure the windows are shown in case of an exception
                this.Show();
                if (webViewWindow != null)
                {
                    webViewWindow.Show();
                }

                MessageBox.Show("Error during EXFIL/TRANSIT processing: " + ex.Message);
            }
        }

        // Method to capture a larger area of the screen
        private Bitmap CaptureExfilTransitArea()
        {
            double dpiScale = GetDpiScaleFactor();

            // Define the area to capture (adjust these values)
            int width = (int)(790 * dpiScale); // Adjust width as needed
            int height = (int)(715 * dpiScale); // Adjust height as needed
            int left = (int)(1765 * dpiScale);  // Adjust left as needed
            int top = (int)(85 * dpiScale);     // Adjust top as needed

            Rectangle captureArea = new Rectangle(left, top, width, height);
            Bitmap bitmap = new Bitmap(captureArea.Width, captureArea.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(captureArea.Left, captureArea.Top, 0, 0, captureArea.Size, CopyPixelOperation.SourceCopy);
            }
            return bitmap;
        }

        // Preprocess image for word detection
        private Bitmap PreprocessImage(Bitmap image)
        {
            // Convert to grayscale
            Grayscale grayscaleFilter = Grayscale.CommonAlgorithms.BT709;
            Bitmap grayImage = grayscaleFilter.Apply(image);

            // Optionally, apply other preprocessing steps

            return grayImage;
        }

        // Locate EXFIL and TRANSIT words
        private List<Rectangle> LocateExfilTransitWords(Bitmap image)
        {
            List<Rectangle> wordRectangles = new List<Rectangle>();
            string tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            string language = "eng+Bender+Bender-Bold";

            try
            {
                using (var engine = new TesseractEngine(tessDataPath, language, EngineMode.Default))
                {
                    // Set page segmentation mode to sparse text
                    engine.DefaultPageSegMode = PageSegMode.SparseText;

                    using (var pix = PixConverter.ToPix(image))
                    {
                        using (var page = engine.Process(pix))
                        {
                            using (var iterator = page.GetIterator())
                            {
                                iterator.Begin();

                                do
                                {
                                    string word = iterator.GetText(PageIteratorLevel.Word);
                                    if (word != null)
                                    {
                                        word = word.Trim().ToUpper();
                                        // Modify the condition to check if the word starts with EXFIL or TRANSIT
                                        MessageBox.Show($"{word}");
                                        if (word.StartsWith("EXFIL") || (word.StartsWith("TRANSIT") && word.Length > 7 ))
                                        {
                                            // Get the bounding box of the word
                                            if (iterator.TryGetBoundingBox(PageIteratorLevel.Word, out Tesseract.Rect boundingBox))
                                            {
                                                // Convert Tesseract's Rect to System.Drawing.Rectangle
                                                Rectangle rect = new Rectangle(boundingBox.X1, boundingBox.Y1, boundingBox.Width, boundingBox.Height);
                                                wordRectangles.Add(rect);
                                            }
                                        }
                                    }
                                } while (iterator.Next(PageIteratorLevel.Word));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error locating EXFIL/TRANSIT words: " + ex.Message);
            }
            // Sort the rectangles by Y-coordinate (top to bottom)
            wordRectangles.Sort((rect1, rect2) => rect1.Y.CompareTo(rect2.Y));

            return wordRectangles;
        }

        // Define rectangles for each entry
        private List<Rectangle> GetEntryRectangles(List<Rectangle> wordRectangles, int imageWidth)
        {
            List<Rectangle> entryRectangles = new List<Rectangle>();

            foreach (var wordRect in wordRectangles)
            {
                // Define padding (adjust as needed)
                int paddingTop = 8;
                int paddingLeft = 8;

                // Calculate the rectangle for the entry
                int x = Math.Max(0, wordRect.X - paddingLeft);
                int y = Math.Max(0, wordRect.Y - paddingTop);
                int width = imageWidth - x;
                int height = 39;

                Rectangle entryRect = new Rectangle(x, y, width, height);
                entryRectangles.Add(entryRect);
            }

            return entryRectangles;
        }

        // Extract and process each entry
        private List<string> ExtractEntryTexts(Bitmap originalImage, List<Rectangle> entryRectangles)
        {
            List<string> entryTexts = new List<string>();

            foreach (var rect in entryRectangles)
            {
                // Crop the entry from the original image
                Bitmap entryImage = CropImage(originalImage, rect);

                // Split the entry image into left 72% and right 28%
                var (leftImage, rightImage) = SplitImage(entryImage);

                // Save the split images for debugging
                leftImage.Save($"entry_left_{rect.X}_{rect.Y}.png", System.Drawing.Imaging.ImageFormat.Png);
                rightImage.Save($"entry_right_{rect.X}_{rect.Y}.png", System.Drawing.Imaging.ImageFormat.Png);

                // Preprocess the left image (if you want to perform OCR on it)
                Bitmap preprocessedLeftImage = PreprocessEntryImage(leftImage);

                // Save the left image for debugging
                preprocessedLeftImage.Save($"entry_{rect.X}_{rect.Y}.png", System.Drawing.Imaging.ImageFormat.Png);

                // Perform OCR on the left image
                string entryText = PerformOCROnEntry(preprocessedLeftImage);

                entryTexts.Add(entryText);
            }

            return entryTexts;
        }

        private (Bitmap leftImage, Bitmap rightImage) SplitImage(Bitmap image)
        {
            int width = image.Width;
            int height = image.Height;

            // Calculate the width for the left and right images
            int leftWidth = (int)(width * 0.72);
            int rightWidth = width - leftWidth;

            // Create rectangles for the left and right images
            Rectangle leftRect = new Rectangle(0, 0, leftWidth, height);
            Rectangle rightRect = new Rectangle(leftWidth, 0, rightWidth, height);

            // Crop the left and right images
            Bitmap leftImage = image.Clone(leftRect, image.PixelFormat);
            Bitmap rightImage = image.Clone(rightRect, image.PixelFormat);

            return (leftImage, rightImage);
        }


        private Bitmap CropImage(Bitmap image, Rectangle rect)
        {
            Bitmap croppedImage = image.Clone(rect, image.PixelFormat);
            return croppedImage;
        }

        private Bitmap PreprocessEntryImage(Bitmap image)
        {
            // Convert to grayscale
            Grayscale grayscaleFilter = Grayscale.CommonAlgorithms.BT709;
            Bitmap grayImage = grayscaleFilter.Apply(image);

            // Apply adaptive thresholding
            OtsuThreshold thresholdFilter = new OtsuThreshold();
            thresholdFilter.ApplyInPlace(grayImage);

            // Resize the image to improve OCR accuracy
            ResizeBilinear resizeFilter = new ResizeBilinear(grayImage.Width * 3, grayImage.Height * 3);
            Bitmap resizedImage = resizeFilter.Apply(grayImage);

            // Apply median filter to reduce noise
            Median medianFilter = new Median();
            Bitmap filteredImage = medianFilter.Apply(resizedImage);

            return filteredImage;
        }

        private string PerformOCROnEntry(Bitmap image)
        {
            string tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            string language = "eng+Bender+Bender-Bold";

            try
            {
                using (var engine = new TesseractEngine(tessDataPath, language, EngineMode.Default))
                {
                    // Set page segmentation mode
                    engine.DefaultPageSegMode = PageSegMode.SingleLine;

                    using (var pix = PixConverter.ToPix(image))
                    {
                        using (var page = engine.Process(pix))
                        {
                            string text = page.GetText();

                            // Display the extracted OCR text for debugging
                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show("Extracted Entry OCR Text:\n" + text);
                            });

                            return text;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error performing OCR on entry: " + ex.Message);
                return string.Empty;
            }
        }

        // Clean up OCR text to correct common misrecognitions
        private string CleanOcrText(string text)
        {
            // Correct common misreadings
            text = text.Replace("EXF1L", "EXFIL");
            text = text.Replace("EXFLL", "EXFIL");
            text = text.Replace("EXFlL", "EXFIL");
            text = text.Replace("EXFIL-", "EXFIL");
            text = text.Replace("TRANS1T", "TRANSIT");
            text = text.Replace("TRANSIT-", "TRANSIT");
            text = text.Replace("TRRANSIT", "TRANSIT");
            text = text.Replace("TRANS'IT", "TRANSIT");

            // Handle cases like "EXFILO3" or "EXFIL03"
            text = Regex.Replace(text, @"EXFILO(\d+)", "EXFIL$1");
            text = Regex.Replace(text, @"EXFIL0(\d+)", "EXFIL$1");
            text = Regex.Replace(text, @"EXFIL@(\d+)", "EXFIL$1");

            // Handle cases like "TRANSITO3" or "TRANSIT03"
            text = Regex.Replace(text, @"TRANSITO(\d+)", "TRANSIT$1");
            text = Regex.Replace(text, @"TRANSIT0(\d+)", "TRANSIT$1");
            text = Regex.Replace(text, @"TRANSIT@(\d+)", "TRANSIT$1");

            // Remove unwanted characters
            text = Regex.Replace(text, @"[^A-Za-z0-9:\?\s-]", "");

            // Normalize whitespace
            text = Regex.Replace(text, @"\s+", " ");

            return text;
        }

        // Update the grid with the parsed entries
        private void UpdateExfilTransitGrid(List<string> entryTexts)
        {
            // Clear existing entries
            ExfilTransitEntries.Clear();

            foreach (var text in entryTexts)
            {
                // Clean up OCR text
                string cleanedText = CleanOcrText(text);

                // Parse the cleaned text to create an ExfilTransitEntry
                ExfilTransitEntry entry = ParseEntryText(cleanedText);

                if (entry != null)
                {
                    ExfilTransitEntries.Add(entry);
                }
            }

            // Update the UI if necessary
            Dispatcher.Invoke(() =>
            {
                // Your code to refresh the UI if needed
            });

            // Display the number of entries
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Number of entries after update: {ExfilTransitEntries.Count}");
            });
        }

        private ExfilTransitEntry ParseEntryText(string text)
        {
            // Adjusted regex pattern
            string pattern = @"\b(EXFIL|TRANSIT)(\d*)\s+(.+?)(?:\s+(\d{1,2}:\d{2}:\d{2}|[\?]{2}:[\?]{2}:[\?]{2}))?$";

            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);

            if (match.Success)
            {
                string type = match.Groups[1].Value.ToUpper();
                string indexStr = match.Groups[2].Value;
                int index = 0;
                if (!string.IsNullOrEmpty(indexStr))
                {
                    index = int.Parse(indexStr);
                }
                string name = match.Groups[3].Value.Trim();
                string timeString = match.Groups[4].Value?.Trim() ?? "";

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

                return entry;
            }
            else
            {
                // Handle parsing failure
                return null;
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

        private string name;
        public string Name
        {
            get { return name; }
            set
            {
                name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

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