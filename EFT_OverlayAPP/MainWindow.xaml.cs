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
        public CraftingWindow craftingWindow;
        private WebViewWindow webViewWindow;
        private OthersWindow othersWindow;
        private RequiredItemsWindow requiredItemsWindow;
        private ConfigWindow configWindow;
        private IntPtr hwnd;
        public ProfileMode LastProfileMode { get; set; }
        public ProfileMode lastVisibleState { get; set; }
        public ProfileMode EffectiveProfileMode { get; set; }

        private DispatcherTimer timer;
        private DispatcherTimer craftsTimer;
        private TimeSpan remainingTime;

        private bool IsRaidTimerRunning = false;

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

        private bool manualOtherWindowButtonsVisibilityOverride = false;
        public bool ManualOtherWindowButtonsVisibilityOverride
        {
            get => manualOtherWindowButtonsVisibilityOverride;
            set
            {
                if (manualOtherWindowButtonsVisibilityOverride != value)
                {
                    manualOtherWindowButtonsVisibilityOverride = value;
                    OnPropertyChanged(nameof(ManualOtherWindowButtonsVisibilityOverride));
                }
            }
        }

        private bool manualCraftingUIVisibilityOverride = false;
        public bool ManualCraftingUIVisibilityOverride
        {
            get => manualCraftingUIVisibilityOverride;
            set
            {
                if (manualCraftingUIVisibilityOverride != value)
                {
                    manualCraftingUIVisibilityOverride = value;
                    OnPropertyChanged(nameof(ManualCraftingUIVisibilityOverride));
                }
            }
        }

        public bool IsEditingKeybind { get; set; }

        // Hotkey IDs
        private const int HOTKEY_ID_RAID_TIMER = 9001; // Unique ID for Raid Timer OCR hotkey
        private const int HOTKEY_ID_CRAFTING_WINDOW = 9002; // Unique ID for CraftingWindow hotkey
        private const int HOTKEY_ID_REQUIRED_ITEMS_WINDOW = 9003;
        private const int HOTKEY_ID_CONFIG_WINDOW = 9004;
        private const int HOTKEY_ID_MINIMAP_VISIBILITY = 9005;
        private const int HOTKEY_ID_RAID_TIMER_VISIBILITY = 9006;
        private const int HOTKEY_ID_CRAFTING_TIMERS_VISIBILITY = 9007;
        private const int HOTKEY_ID_OTHERBUTTONS_WINDOW = 9008;
        private const int HOTKEY_ID_TOGGLE_RAID_TIMER = 9009;

        private HwndSource source;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closed += MainWindow_Closed;
            DataContext = this;

            configWindow = new ConfigWindow(this);

            configWindow.PropertyChanged += ConfigWindow_PropertyChanged;

            UtilizeAndUpdateProfileMode();

            // Start data loading
            Task.Run(() => DataCache.LoadDataAsync(configWindow));

            // Subscribe to DataLoaded event
            DataCache.DataLoaded += OnDataLoaded;

            // Initialize your existing timer or other overlay content here
            InitializeTimer();

            // Initialize the crafts timer
            InitializeCraftsTimer();

            // Start loading data for RequiredItemsWindow
            StartLoadingRequiredItemsData();

            IsRaidTimerVisible = false; // Initialize to false
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                gameStateManager = new GameStateManager(configWindow.AppConfig.EftLogsPath, configWindow);
                gameStateManager.GameStateChanged += GameStateManager_GameStateChanged;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }

            // Get the window handle
            hwnd = new WindowInteropHelper(this).Handle;

            // Make the window click-through
            int exStyle = (int)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)(exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT));

            // Create and show the WebView window
            webViewWindow = new WebViewWindow(this, configWindow, gameStateManager);

            // Show the OthersWindow
            othersWindow = new OthersWindow(this, gameStateManager.GameState, configWindow);
            othersWindow.Show();

            // Register the global hotkeys
            source = HwndSource.FromHwnd(hwnd);
            source.AddHook(HwndHook);
            RegisterConfiguredHotKeys();

            craftingWindow = new CraftingWindow(this, configWindow);
            craftingWindow.Activate();
            craftingWindow.RefreshAllViews();

            HideCraftingUIWhenInRaid = configWindow.AppConfig.HideCraftingUIWhenInRaid;
            configWindow.AppConfig.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(configWindow.AppConfig.HideCraftingUIWhenInRaid))
                {
                    HideCraftingUIWhenInRaid = configWindow.AppConfig.HideCraftingUIWhenInRaid;
                }
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

            if (configWindow != null)
                configWindow.PropertyChanged -= ConfigWindow_PropertyChanged;

            if (gameStateManager != null)
                gameStateManager.GameStateChanged -= GameStateManager_GameStateChanged;
            // Unsubscribe from DataCache events
            DataCache.DataLoaded -= OnDataLoaded;
            if (timer != null)
            {
                timer.Tick -= Timer_Tick;
            }

            if (craftsTimer != null)
            {
                craftsTimer.Tick -= CraftsTimer_Tick;
            }

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
                ActiveCraftTimers.Clear();
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

        private void UnregisterHotKeys()
        {
            UnregisterHotKey(hwnd, HOTKEY_ID_RAID_TIMER);
            UnregisterHotKey(hwnd, HOTKEY_ID_CRAFTING_WINDOW);
            UnregisterHotKey(hwnd, HOTKEY_ID_REQUIRED_ITEMS_WINDOW);
            UnregisterHotKey(hwnd, HOTKEY_ID_CONFIG_WINDOW);
            UnregisterHotKey(hwnd, HOTKEY_ID_MINIMAP_VISIBILITY);
            UnregisterHotKey(hwnd, HOTKEY_ID_RAID_TIMER_VISIBILITY);
            UnregisterHotKey(hwnd, HOTKEY_ID_CRAFTING_TIMERS_VISIBILITY);
            UnregisterHotKey(hwnd, HOTKEY_ID_OTHERBUTTONS_WINDOW);
            UnregisterHotKey(hwnd, HOTKEY_ID_TOGGLE_RAID_TIMER);
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
                if (lastVisibleState != EffectiveProfileMode)
                {
                    requiredItemsWindow.Show();
                    requiredItemsWindow.ReloadData();
                }
                else
                {
                    requiredItemsWindow.Show();
                }    
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
                configWindow = new ConfigWindow(this);
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
            IsRaidTimerRunning = true;
        }

        private void InitializeCraftsTimer()
        {
            craftsTimer = new DispatcherTimer();
            craftsTimer.Interval = TimeSpan.FromSeconds(1);
            craftsTimer.Tick += CraftsTimer_Tick;
            craftsTimer.Start();
            logger.Info("CraftsTimer initialized and started.");
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

            // Check if the remaining time is 10 minutes or less
            if ((remainingTime <= TimeSpan.FromMinutes(10)) && configWindow.AppConfig.HideRaidTimerOn10MinutesLeft)
            {
                IsRaidTimerVisible = false; // Hide the timer
            }

            UpdateTimerText();
        }

        private void CraftsTimer_Tick(object sender, EventArgs e)
        {
            logger.Info("CraftsTimer_Tick invoked.");
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
                TimerTextBlock.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(182, 193, 199)); // Tarkov's default color
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

        public GameStateManager gameStateManager;

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

                // Determine the effective profile mode and use it to update config window
                UtilizeAndUpdateProfileMode();

                // Show or hide the WebViewWindow based on CurrentMap
                if (string.IsNullOrEmpty(CurrentMap))
                {
                    // CurrentMap is null or empty, but is in raid and config option is enabled, show the WebViewWindow
                    if (webViewWindow != null && !webViewWindow.IsVisible && IsInRaid && configWindow.AppConfig.ShowMinimapWhenInRaid)
                    {
                        logger.Info("CurrentMap is null or empty, but is in raid, showing WebViewWindow");
                        webViewWindow.Show();
                    }
                    // CurrentMap is null or empty and isn't in raid and config option is enabled, hide the WebViewWindow
                    if (webViewWindow != null && webViewWindow.IsVisible && !IsInRaid && configWindow.AppConfig.HideMinimapWhenOutOfRaid)
                    {
                        logger.Info("CurrentMap is null or empty, hiding WebViewWindow");
                        webViewWindow.Hide();
                    }
                }
                else
                {
                    // CurrentMap is set, is currently Matching, show the WebViewWindow if config option is enabled
                    if (webViewWindow != null && !webViewWindow.IsVisible && IsMatching && configWindow.AppConfig.ShowMinimapWhenMatching)
                    {
                        logger.Info("CurrentMap is set, showing WebViewWindow");
                        webViewWindow.Show();
                    }
                    // CurrentMap is set, is currently In Raid, show the WebViewWindow if config option is enabled
                    if (webViewWindow != null && !webViewWindow.IsVisible && IsInRaid && configWindow.AppConfig.ShowMinimapWhenInRaid)
                    {
                        logger.Info("CurrentMap is set, showing WebViewWindow");
                        webViewWindow.Show();
                    }
                }

                // Hide the raid timer when not in raid and the config option is enabled
                if (!IsInRaid && configWindow.AppConfig.HideRaidTimerOnRaidEnd)
                {
                    IsRaidTimerVisible = false; 
                }

            });
        }

        private bool hideCraftingUIWhenInRaid;
        public bool HideCraftingUIWhenInRaid
        {
            get => hideCraftingUIWhenInRaid;
            set
            {
                if (hideCraftingUIWhenInRaid != value)
                {
                    hideCraftingUIWhenInRaid = value;
                    OnPropertyChanged(nameof(HideCraftingUIWhenInRaid));
                }
            }
        }

        private void ConfigWindow_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            UtilizeAndUpdateProfileMode();
        }

        public void UtilizeAndUpdateProfileMode(bool slider = false, bool crafting = false)
        {
            // Determine the effective profile mode and use it to update config window
            EffectiveProfileMode = configWindow.AppConfig.SelectedProfileMode;

            if (configWindow.AppConfig.SelectedProfileMode == ProfileMode.Automatic)
            {
                EffectiveProfileMode = SessionMode == SessionMode.Regular ? ProfileMode.Regular : ProfileMode.Pve;
                logger.Info($"Profile mode set to Automatic. Effective Profile Mode: {EffectiveProfileMode}");
            }
            else
            {
                logger.Info($"Profile mode set manually to: {EffectiveProfileMode}");
            }

            switch (EffectiveProfileMode)
            {
                case (ProfileMode.Regular):
                    App.IsPVEMode = false;
                    break;
                case (ProfileMode.Pve):
                    App.IsPVEMode = true;
                    break;
            }
            configWindow.SaveEffectiveProfileMode(EffectiveProfileMode);
            configWindow.ChangeCurrentProfileModeTextBlock_Text(EffectiveProfileMode);
            configWindow.DetermineListContent(EffectiveProfileMode);

            if(requiredItemsWindow != null && requiredItemsWindow.IsVisible)
            {
                lastVisibleState = EffectiveProfileMode;
            }

            // Reload data in RequiredItemsWindow
            if ((LastProfileMode != EffectiveProfileMode || slider) && requiredItemsWindow != null && requiredItemsWindow.IsVisible)
            {
                requiredItemsWindow.ReloadData();
            }

            // Reload data in CraftingWindow
            if ((LastProfileMode != EffectiveProfileMode || slider) && craftingWindow != null)
            {
                craftingWindow.ReloadData();
            }

            if (crafting && craftingWindow != null)
            {
                craftingWindow.ReloadData();
            }

                LastProfileMode = EffectiveProfileMode;
        }

        public void CraftingWindowDataReload()
        {
            if (craftingWindow != null)
            {
                craftingWindow.ReloadData();
            }
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

        public void RegisterConfiguredHotKeys()
        {
            // First, unregister all previously registered hotkeys
            UnregisterHotKeys();

            // Iterate over AppConfig.Keybinds
            foreach (var keybindEntry in configWindow.AppConfig.Keybinds)
            {
                // Parse the keybindEntry.Keybind to extract modifiers and main key
                (uint modifiers, uint virtualKey) = ParseKeybind(keybindEntry.Keybind);

                // Assign a unique ID for each functionality, maybe using a dictionary
                int hotkeyId = GetHotkeyIdForFunctionality(keybindEntry.Functionality);

                if (!RegisterHotKey(hwnd, hotkeyId, modifiers, virtualKey))
                {
                    logger.Warn($"Failed to register hotkey for {keybindEntry.Functionality}.");
                }
                else
                {
                    logger.Info($"Registered hotkey for {keybindEntry.Functionality}: {keybindEntry.Keybind}");
                }
            }
        }

        private (uint modifiers, uint virtualKey) ParseKeybind(string keybind)
        {
            // Example assumes a format: "Ctrl+Shift+T" or "Alt+F1"
            // You can implement a more robust parser here.
            uint mods = 0;
            uint vk = 0;
            var parts = keybind.Split('+');
            foreach (var part in parts)
            {
                try
                {
                    string trimmedPart = part.Trim();
                    switch (trimmedPart.ToLowerInvariant())
                    {
                        case "ctrl":
                            mods |= MOD_CONTROL;
                            break;
                        case "shift":
                            mods |= MOD_SHIFT;
                            break;
                        case "alt":
                            mods |= MOD_ALT;
                            break;
                        default:
                            // Assume this is the main key
                            vk = (uint)KeyInterop.VirtualKeyFromKey((Key)Enum.Parse(typeof(Key), trimmedPart, true));
                            break;
                    }
                }
                catch
                {
                    logger.Warn("Invalid keybind, skipping it.");
                    continue; // Skip this hotkey
                }
            }

            return (mods, vk);
        }

        private Dictionary<string, int> functionalityToHotkeyId = new Dictionary<string, int>
        {
            {"Raid Timer OCR", HOTKEY_ID_RAID_TIMER},
            {"Open Crafting Window", HOTKEY_ID_CRAFTING_WINDOW},
            {"Open Required Items Window", HOTKEY_ID_REQUIRED_ITEMS_WINDOW},
            {"Open Config Window", HOTKEY_ID_CONFIG_WINDOW},
            {"Toggle Minimap Visibility", HOTKEY_ID_MINIMAP_VISIBILITY},
            {"Toggle Raid Timer Visibility", HOTKEY_ID_RAID_TIMER_VISIBILITY},
            {"Toggle Crafting Timers Visibility", HOTKEY_ID_CRAFTING_TIMERS_VISIBILITY},
            {"Toggle OtherWindow Buttons", HOTKEY_ID_OTHERBUTTONS_WINDOW},
            {"Toggle Raid Timer", HOTKEY_ID_TOGGLE_RAID_TIMER}
        };

        private int GetHotkeyIdForFunctionality(string functionality)
        {
            return functionalityToHotkeyId.TryGetValue(functionality, out int id) ? id : -1;
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();

                string functionality = functionalityToHotkeyId.FirstOrDefault(x => x.Value == id).Key;
                if (!string.IsNullOrEmpty(functionality))
                {
                    // Perform action based on functionality
                    switch (functionality)
                    {
                        case "Raid Timer OCR":
                            CaptureAndProcessRaidTimer();
                            handled = true;
                            break;
                        case "Open Crafting Window":
                            OpenCraftingWindow();
                            handled = true;
                            break;
                        case "Open Required Items Window":
                            OpenRequiredItemsWindow();
                            handled = true;
                            break;
                        case "Open Config Window":
                            OpenConfigWindow();
                            handled = true;
                            break;
                        case "Toggle Minimap Visibility":
                            ToggleMinimapVisibility();
                            handled = true;
                            break;
                        case "Toggle Raid Timer Visibility":
                            ToggleRaidTimerVisibility();
                            handled = true;
                            break;
                        case "Toggle Crafting Timers Visibility":
                            ToggleCraftingTimersVisibility();
                            handled = true;
                            break;
                        case "Toggle OtherWindow Buttons":
                            ToggleOtherWindowButtons();
                            handled = true;
                            break;
                        case "Toggle Raid Timer":
                            ToggleRaidTimer();
                            handled = true;
                            break;
                    }
                }
            }
            return IntPtr.Zero;
        }

        public void DisableHotkeysTemporarily()
        {
            IsEditingKeybind = true;
            UnregisterHotKeys();
        }

        public void ReenableHotkeys()
        {
            IsEditingKeybind = false;
            RegisterConfiguredHotKeys();
        }

        private void ToggleMinimapVisibility()
        {
            if (webViewWindow != null)
            {
                if (webViewWindow.IsVisible)
                {
                    webViewWindow.Hide();
                }
                else
                {
                    webViewWindow.Show();
                }
            }
        }

        private void ToggleRaidTimerVisibility()
        {
            IsRaidTimerVisible = !IsRaidTimerVisible;
            OnPropertyChanged(nameof(IsRaidTimerVisible));
        }

        private void ToggleRaidTimer()
        {
            if (remainingTime > TimeSpan.Zero)
            {
                if (IsRaidTimerRunning)
                {
                    timer.Stop();
                    IsRaidTimerRunning = false;
                }
                else
                {
                    timer.Start();
                    IsRaidTimerRunning = true;
                }
            }
            else
            {
                MessageBox.Show("Raid timer is not running.");
            }
        }

        private void ToggleCraftingTimersVisibility()
        {
            ManualCraftingUIVisibilityOverride = !ManualCraftingUIVisibilityOverride;
            OnPropertyChanged(nameof(ManualCraftingUIVisibilityOverride));
        }

        private void ToggleOtherWindowButtons()
        {
            ManualOtherWindowButtonsVisibilityOverride = !ManualOtherWindowButtonsVisibilityOverride;
            OnPropertyChanged(nameof(ManualOtherWindowButtonsVisibilityOverride));
        }
    }
}