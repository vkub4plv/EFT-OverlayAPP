using System;
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
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using NLog;

namespace EFT_OverlayAPP
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        public ObservableCollection<CraftTimerDisplayItem> ActiveCraftTimers { get; set; } = new ObservableCollection<CraftTimerDisplayItem>();
        private CraftingWindow craftingWindow;
        private WebViewWindow webViewWindow;
        private OthersWindow othersWindow;
        private RequiredItemsWindow requiredItemsWindow;
        private ConfigWindow configWindow;
        private IntPtr hwnd;

        private DispatcherTimer timer;
        private DispatcherTimer craftsTimer;
        private TimeSpan remainingTime;

        private bool isRaidTimerVisible;
        public bool IsRaidTimerVisible
        {
            get => isRaidTimerVisible;
            set
            {
                if (isRaidTimerVisible != value)
                {
                    isRaidTimerVisible = value;
                    OnPropertyChanged(nameof(IsRaidTimerVisible));
                }
            }
        }

        // Hotkey IDs
        private const int HOTKEY_ID_RAID_TIMER = 9001; // Unique ID for Raid Timer OCR hotkey
        private const int HOTKEY_ID_CRAFTING_WINDOW = 9002; // Unique ID for CraftingWindow hotkey

        private HwndSource source;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closed += MainWindow_Closed;
            DataContext = this;

            // Subscribe to DataLoaded event
            DataCache.DataLoaded += OnDataLoaded;

            // Initialize your existing timer or other overlay content here
            InitializeTimer();

            // Initialize the crafts timer
            InitializeCraftsTimer();

            // Start loading data for RequiredItemsWindow
            StartLoadingRequiredItemsData();

            try
            {
                string logsDirectory = GamePathHelper.GetLogsDirectory();
                gameStateManager = new GameStateManager(logsDirectory);
                gameStateManager.GameStateChanged += GameStateManager_GameStateChanged;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }

            IsRaidTimerVisible = false; // Initialize to false
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Get the window handle
            hwnd = new WindowInteropHelper(this).Handle;

            // Make the window click-through
            int exStyle = (int)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)(exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT));

            // Create and show the WebView window
            webViewWindow = new WebViewWindow(this, gameStateManager.GameState);

            // Show the OthersWindow
            othersWindow = new OthersWindow(this, gameStateManager.GameState);
            othersWindow.Show();

            // Register the global hotkeys
            source = HwndSource.FromHwnd(hwnd);
            source.AddHook(HwndHook);
            RegisterHotKeys();

            configWindow = new ConfigWindow
            {
                Owner = this
            };
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            // Save quantities from RequiredItemsWindow if it's open
            if (requiredItemsWindow != null)
            {
                requiredItemsWindow.SaveQuantities();
                requiredItemsWindow.SaveManualCombinedQuantities();
            }

            // Close the WebViewWindow
            if (webViewWindow != null)
            {
                webViewWindow.Close();
                webViewWindow = null;
            }

            // Close the OthersWindow
            if (othersWindow != null)
            {
                othersWindow.Close();
                othersWindow = null;
            }

            // Close the RequiredItemsWindow if open
            if (requiredItemsWindow != null)
            {
                requiredItemsWindow.Close();
                requiredItemsWindow = null;
            }

            CloseCraftingWindow();

            // Unregister the hotkeys
            source.RemoveHook(HwndHook);
            UnregisterHotKeys();
        }

        private async void StartLoadingRequiredItemsData()
        {
            await DataCache.LoadRequiredItemsData();
        }

        private void OnDataLoaded()
        {
            Dispatcher.Invoke(() =>
            {
                // Subscribe to property changes and update the UI
                foreach (var item in DataCache.CraftableItems)
                {
                    item.PropertyChanged += CraftableItem_PropertyChanged;

                    if (item.CraftStatus != CraftStatus.NotStarted)
                    {
                        UpdateCraftDisplay(item, remove: false);
                    }
                }
            });
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

            // Hotkey for CraftingWindow (e.g., Ctrl + Shift + C)
            uint modifiersCrafting = MOD_CONTROL | MOD_SHIFT;
            uint virtualKeyCrafting = (uint)KeyInterop.VirtualKeyFromKey(Key.C);

            if (!RegisterHotKey(hwnd, HOTKEY_ID_CRAFTING_WINDOW, modifiersCrafting, virtualKeyCrafting))
            {
                MessageBox.Show("Failed to register hotkey for Crafting Window.");
            }
        }

        private void UnregisterHotKeys()
        {
            UnregisterHotKey(hwnd, HOTKEY_ID_RAID_TIMER);
            UnregisterHotKey(hwnd, HOTKEY_ID_CRAFTING_WINDOW);
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
                else if (id == HOTKEY_ID_CRAFTING_WINDOW)
                {
                    // Handle hotkey press for opening Crafting Window
                    OpenCraftingWindow();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        // Method to open the CraftingWindow
        public void OpenCraftingWindow()
        {
            if (craftingWindow == null)
            {
                craftingWindow = new CraftingWindow(this, configWindow);
            }

            if (!craftingWindow.IsVisible)
            {
                craftingWindow.Show();
            }
            else
            {
                craftingWindow.Activate();
            }
        }

        public void OpenRequiredItemsWindow()
        {
            if (requiredItemsWindow == null)
            {
                requiredItemsWindow = new RequiredItemsWindow(configWindow);
            }

            if (!requiredItemsWindow.IsVisible)
            {
                requiredItemsWindow.Show();
            }
            else
            {
                requiredItemsWindow.Activate();
            }
        }

        public void OpenConfigWindow()
        {
            if (configWindow == null)
            {
                configWindow = new ConfigWindow
                {
                    Owner = this
                };
            }

            if (!configWindow.IsVisible)
            {
                configWindow.Show();
            }
            else
            {
                configWindow.Activate();
            }
        }

        private void CloseCraftingWindow()
        {
            if (craftingWindow != null)
            {
                craftingWindow.Close();
                craftingWindow = null;
            }
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

        private void InitializeCraftsTimer()
        {
            craftsTimer = new DispatcherTimer();
            craftsTimer.Interval = TimeSpan.FromSeconds(1);
            craftsTimer.Tick += CraftsTimer_Tick;
            craftsTimer.Start();
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

        private void CraftsTimer_Tick(object sender, EventArgs e)
        {
            foreach (var displayItem in ActiveCraftTimers)
            {
                displayItem.CraftItem.UpdateRemainingTime();
            }
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
                var hiddenElements = false;
                // Hide the overlay windows
                this.Hide();
                if (webViewWindow != null && webViewWindow.IsVisible)
                {
                    webViewWindow.Hide();
                    hiddenElements = true;
                }

                // Give the system time to refresh the screen without the overlay
                await Task.Delay(500); // Adjust the delay as needed

                // Capture the area with the Raid Timer
                Bitmap raidTimerScreenshot = CaptureRaidTimerArea();

                // Show the overlay windows again
                this.Show();
                if (webViewWindow != null && hiddenElements)
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
                                logger.Info("Extracted OCR Text:\n" + text);
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
                    Dispatcher.Invoke(() =>
                    {
                        UpdateTimerText();
                        IsRaidTimerVisible = true; // Show the timer after OCR
                    });
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

        // Clean up OCR text to correct common misrecognitions
        private string CleanOcrText(string text)
        {
            // Correct common misreadings
            text = text.Replace("O", "0");
            text = text.Replace("o", "0");
            text = text.Replace("l", "1");
            text = text.Replace("I", "1");
            text = text.Replace(";", ":");

            // Remove unwanted characters
            text = Regex.Replace(text, @"[^0-9:]", "");

            return text;
        }

        // Preprocess image before OCR
        private Bitmap PreprocessImage(Bitmap image)
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

        public void UpdateCraftDisplay(CraftableItem item, bool remove)
        {
            Dispatcher.Invoke(() =>
            {
                if (remove)
                {
                    logger.Info($"Removing craft display for Station {item.Station}");
                    var displayItem = ActiveCraftTimers.FirstOrDefault(x => x.Station == item.Station);
                    if (displayItem != null)
                    {
                        ActiveCraftTimers.Remove(displayItem);
                    }
                }
                else
                {
                    logger.Info($"Updating craft display for Station {item.Station}");
                    var displayItem = ActiveCraftTimers.FirstOrDefault(x => x.Station == item.Station);
                    if (displayItem == null)
                    {
                        // Load the station icon
                        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StationIcons", $"{item.Station}.png");
                        ImageSource icon = null;
                        if (File.Exists(iconPath))
                        {
                            icon = new BitmapImage(new Uri(iconPath));
                        }
                        else
                        {
                            // Handle missing icon - use a default icon
                            var defaultIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StationIcons", "default.png");
                            if (File.Exists(defaultIconPath))
                            {
                                icon = new BitmapImage(new Uri(defaultIconPath));
                            }
                        }

                        displayItem = new CraftTimerDisplayItem
                        {
                            Station = item.Station,
                            StationIcon = icon,
                            CraftItem = item
                        };

                        ActiveCraftTimers.Add(displayItem);

                        // Subscribe to property changes
                        item.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(CraftableItem.RemainingTime) ||
                                e.PropertyName == nameof(CraftableItem.CraftStatus))
                            {
                                displayItem.OnPropertyChanged(nameof(CraftTimerDisplayItem.RemainingTimeString));
                                displayItem.OnPropertyChanged(nameof(CraftTimerDisplayItem.RemainingTime));
                            }
                        };
                    }
                }
            });
        }

        private readonly GameStateManager gameStateManager;

        private bool isMatching;
        public bool IsMatching
        {
            get => isMatching;
            set
            {
                if (isMatching != value)
                {
                    isMatching = value;
                    OnPropertyChanged(nameof(IsMatching));
                }
            }
        }

        private bool isInRaid;
        public bool IsInRaid
        {
            get => isInRaid;
            set
            {
                if (isInRaid != value)
                {
                    isInRaid = value;
                    OnPropertyChanged(nameof(IsInRaid));
                }
            }
        }

        private string currentMap;
        public string CurrentMap
        {
            get => currentMap;
            set
            {
                if (currentMap != value)
                {
                    currentMap = value;
                    OnPropertyChanged(nameof(CurrentMap));
                }
            }
        }

        private SessionMode sessionMode;
        public SessionMode SessionMode
        {
            get => sessionMode;
            set
            {
                if (sessionMode != value)
                {
                    sessionMode = value;
                    OnPropertyChanged(nameof(SessionMode));
                }
            }
        }

        private void GameStateManager_GameStateChanged(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                IsMatching = gameStateManager.GameState.IsMatching;
                IsInRaid = gameStateManager.GameState.IsInRaid;
                CurrentMap = gameStateManager.GameState.CurrentMap;
                SessionMode = gameStateManager.GameState.SessionMode;

                logger.Info($"GameState changed: IsInRaid={IsInRaid}, IsMatching={IsMatching}, CurrentMap='{CurrentMap}', SessionMode='{SessionMode}'");

                // Show or hide the WebViewWindow based on CurrentMap
                if (string.IsNullOrEmpty(CurrentMap))
                {
                    // CurrentMap is null or empty, but is in raid, show the WebViewWindow
                    if (webViewWindow != null && !webViewWindow.IsVisible && IsInRaid)
                    {
                        logger.Info("CurrentMap is null or empty, but is in raid, showing WebViewWindow");
                        webViewWindow.Show();
                    }
                    // CurrentMap is null or empty and isn't in raid, hide the WebViewWindow
                    if (webViewWindow != null && webViewWindow.IsVisible && !IsInRaid)
                    {
                        logger.Info("CurrentMap is null or empty, hiding WebViewWindow");
                        webViewWindow.Hide();
                    }
                }
                else
                {
                    logger.Info("CurrentMap is set, showing WebViewWindow");
                    // CurrentMap is set, show the WebViewWindow
                    if (webViewWindow != null && !webViewWindow.IsVisible)
                    {
                        webViewWindow.Show();
                    }
                }

                // Hide the raid timer when not in raid
                if (!IsInRaid)
                {
                    IsRaidTimerVisible = false;
                }

            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void CraftableItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var item = sender as CraftableItem;
            if (item != null)
            {
                if (e.PropertyName == nameof(CraftableItem.CraftStatus))
                {
                    if (item.CraftStatus == CraftStatus.InProgress || item.CraftStatus == CraftStatus.Ready)
                    {
                        UpdateCraftDisplay(item, remove: false);
                    }
                    else if (item.CraftStatus == CraftStatus.NotStarted)
                    {
                        UpdateCraftDisplay(item, remove: true);
                    }
                }
            }
        }

    }
}