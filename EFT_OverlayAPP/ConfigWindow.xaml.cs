using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.IO;
using NLog;
using Ookii.Dialogs.Wpf;
using System.ComponentModel;
using Microsoft.Win32;

namespace EFT_OverlayAPP
{
    public partial class ConfigWindow : Window, INotifyPropertyChanged
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private const string ConfigFilePath = "config.json";
        public ObservableCollection<KeybindEntry> Keybinds { get; set; }
        public AppConfig AppConfig { get; set; }
        private MainWindow MainWindow { get; set; }
        private DebounceDispatcher debounceDispatcher = new DebounceDispatcher(1000); // 1 second debounce
        private const string DefaultLogsFolderName = "Logs";
        private TarkovTrackerService tarkovTrackerService;
        public ConfigWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            LoadConfig();
            this.DataContext = AppConfig;
            MainWindow = mainWindow;
            InitializeMonitorList();
            InitializeStartingTabs();

            // Initialize the Tarkov Tracker Service
            tarkovTrackerService = new TarkovTrackerService(AppConfig);

            this.Loaded += ConfigWindow_Loaded; // Subscribe to Loaded event

            // Subscribe to PropertyChanged for auto-saving
            AppConfig.PropertyChanged += AppConfig_PropertyChanged;
            foreach (var entry in AppConfig.Keybinds)
            {
                entry.PropertyChanged += KeybindEntry_PropertyChanged;
            }

            // Subscribe to CollectionChanged for HideoutModuleSettings
            AppConfig.HideoutModuleSettings.CollectionChanged += HideoutModuleSettings_CollectionChanged;
            // Subscribe to CollectionChanged for CraftModuleSettings
            AppConfig.CraftModuleSettings.CollectionChanged += CraftModuleSettings_CollectionChanged;
            // Subscribe to CollectionChanged for HideoutModuleSettingsPVE
            AppConfig.HideoutModuleSettingsPVE.CollectionChanged += HideoutModuleSettings_CollectionChanged;
            // Subscribe to CollectionChanged for CraftModuleSettingsPVE
            AppConfig.CraftModuleSettingsPVE.CollectionChanged += CraftModuleSettings_CollectionChanged;
            // Subscribe to CollectionChanged for Keybinds
            AppConfig.Keybinds.CollectionChanged += Keybinds_CollectionChanged;
            // Subscribe to events
            tarkovTrackerService.TokenValidated += TarkovTrackerService_TokenValidated;
            tarkovTrackerService.TokenInvalid += TarkovTrackerService_TokenInvalid;
            tarkovTrackerService.ProgressRetrieved += TarkovTrackerService_ProgressRetrieved;

            // Validate the token on startup
            if (AppConfig.IsTarkovTrackerApiEnabled)
            {
                ValidateApiTokenAsync();
            }
        }

        private void ConfigWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MainWindow.UtilizeAndUpdateProfileMode(slider: false);
        }

        private void LoadConfig()
        {
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    AppConfig = JsonConvert.DeserializeObject<AppConfig>(json);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to load configuration. Initializing with default settings.");
                    AppConfig = GetDefaultConfig();
                }
            }
            else
            {
                AppConfig = GetDefaultConfig();
            }

            // Initialize UI elements with loaded settings

            // Keybinds
            if (AppConfig.Keybinds != null)
            {
                KeybindsListView.ItemsSource = AppConfig.Keybinds;
            }

            // Map Website ComboBox selection
            if (!string.IsNullOrEmpty(AppConfig.SelectedMapWebsite))
            {
                foreach (var item in MapWebsiteComboBox.Items)
                {
                    if ((item as ComboBoxItem)?.Content.ToString() == AppConfig.SelectedMapWebsite)
                    {
                        MapWebsiteComboBox.SelectedItem = item;
                        break;
                    }
                }
            }

            // Set the Profile Mode ComboBox based on AppConfig
            SetProfileModeComboBoxSelection();
        }

        private AppConfig GetDefaultConfig()
        {
            return new AppConfig
            {
                Keybinds = new ObservableCollection<KeybindEntry>
                {
                    new KeybindEntry { Functionality = "Raid Timer OCR", Keybind = "F1" },
                    new KeybindEntry { Functionality = "Open Crafting Window", Keybind = "F2" },
                    new KeybindEntry { Functionality = "Open Required Items Window", Keybind = "F3" },
                    new KeybindEntry { Functionality = "Open Config Window", Keybind = "F4" },
                    new KeybindEntry { Functionality = "Toggle Minimap Visibility", Keybind = "F5" },
                    new KeybindEntry { Functionality = "Toggle Raid Timer Visibility", Keybind = "F6" },
                    new KeybindEntry { Functionality = "Toggle Crafting Timers Visibility", Keybind = "F7" },
                    new KeybindEntry { Functionality = "Toggle OtherWindow Buttons", Keybind = "F8" },
                    // Add more keybinds as needed
                },
                IsTarkovTrackerApiEnabled = false,
                SelectedMapWebsite = "Map Genie",
                PvpApiKey = "",
                PveApiKey = "",
                ToggleMinimapVisibility = true,
                ToggleRaidTimerVisibility = false,
                ToggleCraftingTimersVisibility = true,
                ToggleOtherWindowButtons = true,
                CurrentCraftingLevel = 0,
                CurrentCraftingLevelPVE = 0,
                DisableAutoHideRaidTimer = false,
                UseCustomEftLogsPath = false,
                EftLogsPath = GetDefaultEftLogsPath(),
                SelectedProfileMode = ProfileMode.Automatic, // Default to Automatic
                AutoSetActiveMinimap = true,
                FilterBasedOnHideoutLevels = true,
                HideLockedQuestRecipes = true,
                HideTimerOn10MinutesLeft = true,
                HideTimerOnRaidEnd = true,
                HideItemsForBuiltStations = false,
                HideItemsForCompletedQuests = false,
                HidePlantItemsMarkers = false,
                HideQuestsHideoutModulesNames = false,
                SubtractFromManualCombinedItems = false,
                HideoutModuleSettings = new ObservableCollection<HideoutModuleSetting>(),
                CraftModuleSettings = new ObservableCollection<CraftModuleSetting>(),
                HideoutModuleSettingsPVE = new ObservableCollection<HideoutModuleSetting>(),
                CraftModuleSettingsPVE = new ObservableCollection<CraftModuleSetting>(),
                IsManualHideoutSource = true,
                IsManualCraftSource = true,
                CraftingStartingTab = "All Items",
                RequiredItemsStartingTab = "Required Items"
                // Initialize other settings as needed
            };
        }

        private void SaveConfig()
        {
            // Update AppConfig with current UI settings via Data Binding

            // Validate crafting level
            int craftingLevel = AppConfig.CurrentCraftingLevel;
            if (craftingLevel < 0 || craftingLevel > 51)
            {
                MessageBox.Show("Crafting level must be between 0 and 51.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // Validate crafting level PVE
            int craftingLevelPVE = AppConfig.CurrentCraftingLevelPVE;
            if (craftingLevelPVE < 0 || craftingLevelPVE > 51)
            {
                MessageBox.Show("Crafting level for PVE must be between 0 and 51.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate API Keys if enabled
            if (AppConfig.IsTarkovTrackerApiEnabled)
            {
                if (string.IsNullOrWhiteSpace(AppConfig.PvpApiKey) && string.IsNullOrWhiteSpace(AppConfig.PveApiKey))
                {
                    MessageBox.Show("Either PVP or PVE API keys must be provided when Tarkov Tracker API is enabled.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // Selected Map Website
            AppConfig.SelectedMapWebsite = (MapWebsiteComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

            // Save the SelectedTab in Crafting and Required Items if needed
            // Assuming it's already bound via Data Binding

            // Any other settings are already bound via Data Binding

            try
            {
                string json = JsonConvert.SerializeObject(AppConfig, Formatting.Indented);
                File.WriteAllText(ConfigFilePath, json);
                logger.Info("Configuration saved successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                logger.Error(ex, "Failed to save configuration.");
            }
        }

        private string GetDefaultEftLogsPath()
        {
            try
            {
                string installPath = GetGameInstallPath();
                string logsDirectory = System.IO.Path.Combine(installPath, DefaultLogsFolderName);

                if (Directory.Exists(logsDirectory))
                {
                    return logsDirectory;
                }
                else
                {
                    throw new DirectoryNotFoundException($"Logs directory not found at: {logsDirectory}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not find the default EFT logs directory. You will need to select it manually.\n\nDetails: {ex.Message}",
                    "Logs Directory Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);

                // Prompt the user to manually select the logs path
                string selectedPath = PromptForLogsPath();
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    // Enable "Use Custom Path" and set the selected path
                    AppConfig.UseCustomEftLogsPath = true;
                    AppConfig.EftLogsPath = selectedPath;
                    SaveConfig();
                    return selectedPath;
                }

                // Default to an empty path if no selection is made
                return string.Empty;
            }
        }

        private string GetGameInstallPath()
        {
            string gamePath = Properties.Settings.Default.GameInstallPath;

            if (!string.IsNullOrEmpty(gamePath) && Directory.Exists(gamePath))
            {
                return gamePath;
            }

            // Check the registry for the install path
            string uninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\EscapeFromTarkov";

            // Check 64-bit registry
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(uninstallKey))
            {
                if (key != null)
                {
                    object path = key.GetValue("InstallLocation");
                    if (path != null)
                    {
                        gamePath = path.ToString();
                    }
                }
            }

            // Check 32-bit registry if not found in 64-bit
            if (string.IsNullOrEmpty(gamePath))
            {
                uninstallKey = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\EscapeFromTarkov";
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(uninstallKey))
                {
                    if (key != null)
                    {
                        object path = key.GetValue("InstallLocation");
                        if (path != null)
                        {
                            gamePath = path.ToString();
                        }
                    }
                }
            }

            // Prompt the user to select the install path if still not found
            if (string.IsNullOrEmpty(gamePath))
            {
                gamePath = PromptForGamePath();
                if (!string.IsNullOrEmpty(gamePath))
                {
                    Properties.Settings.Default.GameInstallPath = gamePath;
                    Properties.Settings.Default.Save();
                }
            }

            return gamePath;
        }

        private string PromptForLogsPath()
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = "Please select the directory where Escape from Tarkov logs are located."
            };

            bool? result = dialog.ShowDialog();
            return result == true && Directory.Exists(dialog.SelectedPath) ? dialog.SelectedPath : string.Empty;
        }

        private string PromptForGamePath()
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = "Could not determine Escape from Tarkov installation directory. Please select it manually."
            };

            bool? result = dialog.ShowDialog();
            return result == true && Directory.Exists(dialog.SelectedPath) ? dialog.SelectedPath : string.Empty;
        }

        private void UseCustomEftLogsPath_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppConfig.UseCustomEftLogsPath))
            {
                if (AppConfig.UseCustomEftLogsPath)
                {
                    EftLogsPathTextBox.IsEnabled = true;
                }
                else
                {
                    EftLogsPathTextBox.IsEnabled = false;
                    AppConfig.EftLogsPath = GetDefaultEftLogsPath();
                }
            }
        }

        private void BrowseEftLogsPathButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedPath = PromptForLogsPath();
            if (!string.IsNullOrEmpty(selectedPath))
            {
                AppConfig.UseCustomEftLogsPath = true;
                AppConfig.EftLogsPath = selectedPath;
                SaveConfig();
            }
        }

        private async void PvpApiKeySaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Update the Tarkov Tracker Service token
            tarkovTrackerService.UpdateToken();

            // Optionally, validate the token again
            await tarkovTrackerService.ValidateTokenAsync();
        }

        private async void PveApiKeySaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Update the Tarkov Tracker Service token
            tarkovTrackerService.UpdateToken();

            // Optionally, validate the token again
            await tarkovTrackerService.ValidateTokenAsync();
        }

        private void InitializeMonitorList()
        {
            // Placeholder: Populate MonitorSelectionComboBox with detected monitors
            // This will be implemented with actual monitor detection logic
            MonitorSelectionComboBox.Items.Clear();
            MonitorSelectionComboBox.Items.Add("Monitor 1");
            MonitorSelectionComboBox.Items.Add("Monitor 2");
            MonitorSelectionComboBox.Items.Add("Monitor 3");
            // Add more monitors as needed
            // Set default selection
            MonitorSelectionComboBox.SelectedIndex = 0;
            CurrentMonitorTextBlock.Text = MonitorSelectionComboBox.SelectedItem as string;
        }

        private void InitializeStartingTabs()
        {
            // Populate CraftingStartingTabComboBox and RequiredItemsStartingTabComboBox
            // These should reflect the actual tabs available in Crafting and Required Items windows

            // Example for CraftingStartingTabComboBox
            CraftingStartingTabComboBox.Items.Clear();
            CraftingStartingTabComboBox.Items.Add("All Items");
            CraftingStartingTabComboBox.Items.Add("Favorites");
            CraftingStartingTabComboBox.Items.Add("Active Crafts");
            CraftingStartingTabComboBox.Items.Add("Logs");
            CraftingStartingTabComboBox.Items.Add("Stats");
            CraftingStartingTabComboBox.SelectedIndex = 0;

            // Example for RequiredItemsStartingTabComboBox
            RequiredItemsStartingTabComboBox.Items.Clear();
            RequiredItemsStartingTabComboBox.Items.Add("Required Items");
            RequiredItemsStartingTabComboBox.Items.Add("Combined Required Items");
            RequiredItemsStartingTabComboBox.Items.Add("Manual Combined Required Items");
            RequiredItemsStartingTabComboBox.SelectedIndex = 0;
        }

        // Event Handler for Profile Mode Selection
        private void ProfileModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileModeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string selectedContent = selectedItem.Content.ToString();
                switch (selectedContent)
                {
                    case "Automatic":
                        AppConfig.SelectedProfileMode = ProfileMode.Automatic;
                        logger.Info("Profile mode set to Automatic.");
                        break;
                    case "Regular (PVP)":
                        AppConfig.SelectedProfileMode = ProfileMode.Regular;
                        logger.Info("Profile mode set to Regular (PVP).");
                        break;
                    case "PVE":
                        AppConfig.SelectedProfileMode = ProfileMode.Pve;
                        logger.Info("Profile mode set to PVE.");
                        break;
                    default:
                        AppConfig.SelectedProfileMode = ProfileMode.Automatic;
                        logger.Warn("Unknown profile mode selected. Defaulting to Automatic.");
                        break;
                }

                // Notify of property change SelectedProfileMode
                OnPropertyChanged(nameof(AppConfig.SelectedProfileMode));

                // Optionally, trigger changes based on profile mode
            }
        }

        public void ChangeCurrentProfileModeTextBlock_Text(ProfileMode effectiveProfileMode)
        {
            switch (effectiveProfileMode)
            {
                case ProfileMode.Regular:
                    CurrentProfileModeTextBlock.Text = "Regular (PVP)";
                    logger.Info("Profile mode textblock set to Regular (PVP).");
                    break;
                case ProfileMode.Pve:
                    CurrentProfileModeTextBlock.Text = "PVE";
                    logger.Info("Profile mode textblock set to PVE.");
                    break;
            }
        }

        public async void DetermineListContent(ProfileMode effectiveProfileMode)
        {
            switch (effectiveProfileMode)
            {
                case ProfileMode.Regular:
                    await InitializeHideoutModulesAsync();
                    await LoadCraftModuleSettingsAsync();
                    logger.Info("Loaded lists with Regular profile.");
                    break;
                case ProfileMode.Pve:
                    await InitializeHideoutModulesAsyncPVE();
                    await LoadCraftModuleSettingsAsyncPVE();
                    logger.Info("Loaded lists with PVE profile.");
                    break;
            }
        }

        public void SaveEffectiveProfileMode(ProfileMode effectiveProfileMode)
        {
            switch (effectiveProfileMode)
            {
                case ProfileMode.Regular:
                    AppConfig.EffectiveProfileMode = ProfileMode.Regular;
                    logger.Info("Loaded lists with Regular profile.");
                    break;
                case ProfileMode.Pve:
                    AppConfig.EffectiveProfileMode = ProfileMode.Pve;
                    logger.Info("Loaded lists with PVE profile.");
                    break;
            }
        }

        // Event Handler for Crafting Level Slider
        private void CraftingLevelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CraftingLevelDisplay != null)
            {
                int level = (int)e.NewValue;
                CraftingLevelDisplay.Text = $"Current Level: {level}";
                MainWindow.UtilizeAndUpdateProfileMode(slider: true);
            }
        }

        private void CraftingLevelSliderPVE_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CraftingLevelDisplayPVE != null)
            {
                int level = (int)e.NewValue;
                CraftingLevelDisplayPVE.Text = $"Current Level: {level}";
                MainWindow.UtilizeAndUpdateProfileMode(slider: true);
            }
        }

        // Event Handlers for Required Items Tab
        private void ResetPvpProfileRequiredItemsButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement logic to reset PVP profile required items
            // This could involve resetting specific properties in AppConfig
            AppConfig.HideItemsForCompletedQuests = false;
            AppConfig.HideQuestsHideoutModulesNames = false;
            SaveConfig();
            MessageBox.Show("PVP profile required items have been reset.", "Reset Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ResetPveProfileRequiredItemsButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement logic to reset PVE profile required items
            // This could involve resetting specific properties in AppConfig
            AppConfig.HideItemsForCompletedQuests = false;
            AppConfig.HideQuestsHideoutModulesNames = false;
            SaveConfig();
            MessageBox.Show("PVE profile required items have been reset.", "Reset Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task LoadCraftModuleSettingsAsync()
        {
            try
            {
                var craftModules = await DataCache.FetchCraftModuleSettingsAsync();

                // Populate AppConfig.CraftModuleSettings
                foreach (var craftModule in craftModules)
                {
                    // Check if the craft already exists in the settings to prevent duplicates
                    if (!AppConfig.CraftModuleSettings.Any(cm => cm.CraftId == craftModule.CraftId))
                    {
                        AppConfig.CraftModuleSettings.Add(craftModule);
                    }
                }

                // Refresh the ListView binding
                UnlockableCraftsListView.ItemsSource = null;
                UnlockableCraftsListView.ItemsSource = AppConfig.CraftModuleSettings;

                logger.Info("Craft module settings loaded successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load craft module settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                logger.Error(ex, "Failed to load craft module settings.");
            }
        }

        private async Task LoadCraftModuleSettingsAsyncPVE()
        {
            try
            {
                var craftModules = await DataCache.FetchCraftModuleSettingsAsync();

                // Populate AppConfig.CraftModuleSettingsPVE
                foreach (var craftModule in craftModules)
                {
                    // Check if the craft already exists in the settings to prevent duplicates
                    if (!AppConfig.CraftModuleSettingsPVE.Any(cm => cm.CraftId == craftModule.CraftId))
                    {
                        AppConfig.CraftModuleSettingsPVE.Add(craftModule);
                    }
                }

                // Refresh the ListView binding
                UnlockableCraftsListView.ItemsSource = null;
                UnlockableCraftsListView.ItemsSource = AppConfig.CraftModuleSettingsPVE;

                logger.Info("Craft module settings loaded successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load craft module settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                logger.Error(ex, "Failed to load craft module settings.");
            }
        }

        // Event Handler for PropertyChanged events in HideoutModuleSettings
        private void HideoutModuleSetting_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HideoutModuleSetting.SelectedLevel))
            {
                // Debounce the save operation
                debounceDispatcher.Debounce(() => SaveConfig());
            }
        }

        private async Task InitializeHideoutModulesAsync()
        {
            try
            {
                await DataCache.LoadRequiredItemsData();

                if (DataCache.HideoutStations == null || !DataCache.HideoutStations.Any())
                {
                    MessageBox.Show("No hideout stations data available to initialize.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    logger.Error("Hideout stations data is empty during initialization.");
                    return;
                }

                // Preserve existing settings
                var existingSettingsDict = AppConfig.HideoutModuleSettings.ToDictionary(h => h.ModuleName, h => h.SelectedLevel);

                // Clear existing settings to prevent duplicates
                AppConfig.HideoutModuleSettings.Clear();

                foreach (var station in DataCache.HideoutStations)
                {
                    var levels = station.Levels.Select(l => l.Level).OrderBy(l => l).ToList();

                    // Insert 0 for unbuilt
                    var availableLevels = new List<int> { 0 };
                    availableLevels.AddRange(levels);

                    // Determine selected level
                    int selectedLevel = 0; // default to unbuilt

                    if (existingSettingsDict.TryGetValue(station.Name, out int level))
                    {
                        if (level == 0 || levels.Contains(level))
                        {
                            selectedLevel = level;
                        }
                        else
                        {
                            selectedLevel = levels.Min();
                        }
                    }

                    var moduleSetting = new HideoutModuleSetting
                    {
                        ModuleName = station.Name,
                        StationImageLink = station.ImageLink,
                        SelectedLevel = selectedLevel
                    };

                    // Populate AvailableLevels
                    foreach (var lvl in availableLevels)
                    {
                        moduleSetting.AvailableLevels.Add(lvl);
                    }

                    // UnSubscribe from PropertyChanged event to prevent memory leaks
                    moduleSetting.PropertyChanged -= HideoutModuleSetting_PropertyChanged;

                    // Subscribe to PropertyChanged event for automatic saving
                    moduleSetting.PropertyChanged += HideoutModuleSetting_PropertyChanged;

                    AppConfig.HideoutModuleSettings.Add(moduleSetting);
                }

                // Refresh the ListView binding
                HideoutModulesListView.ItemsSource = null;
                HideoutModulesListView.ItemsSource = AppConfig.HideoutModuleSettings;

                logger.Info("Hideout modules initialized successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize hideout modules: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                logger.Error(ex, "Failed to initialize hideout modules.");
            }
        }

        private async Task InitializeHideoutModulesAsyncPVE()
        {
            try
            {
                await DataCache.LoadRequiredItemsData();

                if (DataCache.HideoutStations == null || !DataCache.HideoutStations.Any())
                {
                    MessageBox.Show("No hideout stations data available to initialize.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    logger.Error("Hideout stations data is empty during initialization.");
                    return;
                }

                // Preserve existing settings
                var existingSettingsDict = AppConfig.HideoutModuleSettingsPVE.ToDictionary(h => h.ModuleName, h => h.SelectedLevel);

                // Clear existing settings to prevent duplicates
                AppConfig.HideoutModuleSettingsPVE.Clear();

                foreach (var station in DataCache.HideoutStations)
                {
                    var levels = station.Levels.Select(l => l.Level).OrderBy(l => l).ToList();

                    // Insert 0 for unbuilt
                    var availableLevels = new List<int> { 0 };
                    availableLevels.AddRange(levels);

                    // Determine selected level
                    int selectedLevel = 0; // default to unbuilt

                    if (existingSettingsDict.TryGetValue(station.Name, out int level))
                    {
                        if (level == 0 || levels.Contains(level))
                        {
                            selectedLevel = level;
                        }
                        else
                        {
                            selectedLevel = levels.Min();
                        }
                    }

                    var moduleSetting = new HideoutModuleSetting
                    {
                        ModuleName = station.Name,
                        StationImageLink = station.ImageLink,
                        SelectedLevel = selectedLevel
                    };

                    // Populate AvailableLevels
                    foreach (var lvl in availableLevels)
                    {
                        moduleSetting.AvailableLevels.Add(lvl);
                    }

                    // UnSubscribe from PropertyChanged event to prevent memory leaks
                    moduleSetting.PropertyChanged -= HideoutModuleSetting_PropertyChanged;

                    // Subscribe to PropertyChanged event for automatic saving
                    moduleSetting.PropertyChanged += HideoutModuleSetting_PropertyChanged;

                    AppConfig.HideoutModuleSettingsPVE.Add(moduleSetting);
                }

                // Refresh the ListView binding
                HideoutModulesListView.ItemsSource = null;
                HideoutModulesListView.ItemsSource = AppConfig.HideoutModuleSettingsPVE;

                logger.Info("Hideout modules initialized successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize hideout modules: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                logger.Error(ex, "Failed to initialize hideout modules.");
            }
        }

        // Event Handler for Refresh Hideout Modules Button
        private async void RefreshHideoutModulesButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.UtilizeAndUpdateProfileMode(slider: false);
        }

        // Event Handlers for Unlock Overlay Options
        private void UnlockSavePositionsButton_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder: Unlock and save overlay items positions
            MessageBox.Show("Overlay items positions unlocked and saved.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ResetPositionsButton_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder: Reset overlay items positions to default
            MessageBox.Show("Overlay items positions reset to default.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        // Handling UseCustomEftLogsPathCheckBox
        private void UseCustomEftLogsPathCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            EftLogsPathTextBox.IsEnabled = true;
        }

        private void UseCustomEftLogsPathCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            EftLogsPathTextBox.IsEnabled = false;
            // Reset to default path
            EftLogsPathTextBox.Text = GetDefaultEftLogsPath();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            // Cancel the closing event
            e.Cancel = true;

            // Hide the window instead of closing
            this.Hide();

            SaveConfig();
        }

        // Event Handlers for Monitor Settings
        private void MonitorSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MonitorSelectionComboBox.SelectedItem != null)
            {
                CurrentMonitorTextBlock.Text = MonitorSelectionComboBox.SelectedItem as string;
            }
        }

        private void UseCustomResolutionCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CustomResolutionComboBox.IsEnabled = true;
        }

        private void UseCustomResolutionCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CustomResolutionComboBox.IsEnabled = false;
            // Reset to detected resolution if necessary
        }

        private void ResetKeybindsButton_Click(object sender, RoutedEventArgs e)
        {
            // Confirm the reset action with the user
            var result = MessageBox.Show("Are you sure you want to reset all keybinds to their default values?",
                                         "Confirm Reset",
                                         MessageBoxButton.YesNo,
                                         MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Initialize default keybinds
                AppConfig.Keybinds = GetDefaultKeybinds();

                // Refresh the ListView to display default keybinds
                KeybindsListView.ItemsSource = null;
                KeybindsListView.ItemsSource = AppConfig.Keybinds;

                MainWindow?.RegisterConfiguredHotKeys();

                logger.Info("Keybinds have been reset to default.");
                MessageBox.Show("Keybinds have been reset to their default values.",
                                "Reset Successful",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
            }
        }

        // Helper method to get default keybinds
        private ObservableCollection<KeybindEntry> GetDefaultKeybinds()
        {
            return new ObservableCollection<KeybindEntry>
                {
                    new KeybindEntry { Functionality = "Raid Timer OCR", Keybind = "F1" },
                    new KeybindEntry { Functionality = "Open Crafting Window", Keybind = "F2" },
                    new KeybindEntry { Functionality = "Open Required Items Window", Keybind = "F3" },
                    new KeybindEntry { Functionality = "Open Config Window", Keybind = "F4" },
                    new KeybindEntry { Functionality = "Toggle Minimap Visibility", Keybind = "F5" },
                    new KeybindEntry { Functionality = "Toggle Raid Timer Visibility", Keybind = "F6" },
                    new KeybindEntry { Functionality = "Toggle Crafting Timers Visibility", Keybind = "F7" },
                    new KeybindEntry { Functionality = "Toggle OtherWindow Buttons", Keybind = "F8" },
                    // Add more default keybinds as needed
                };
        }

        private async void ResetToDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            // Confirm the reset action with the user
            var result = MessageBox.Show("Are you sure you want to reset all configuration settings to their default values? This will delete the current configuration file.",
                                         "Confirm Reset",
                                         MessageBoxButton.YesNo,
                                         MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Unsubscribe from existing events if AppConfig is not null
                    if (AppConfig != null)
                    {
                        AppConfig.PropertyChanged -= AppConfig_PropertyChanged;
                        AppConfig.HideoutModuleSettings.CollectionChanged -= HideoutModuleSettings_CollectionChanged;
                        AppConfig.CraftModuleSettings.CollectionChanged -= CraftModuleSettings_CollectionChanged;
                        AppConfig.HideoutModuleSettingsPVE.CollectionChanged -= HideoutModuleSettings_CollectionChanged;
                        AppConfig.CraftModuleSettingsPVE.CollectionChanged -= CraftModuleSettings_CollectionChanged;
                        AppConfig.Keybinds.CollectionChanged -= Keybinds_CollectionChanged;
                        tarkovTrackerService.TokenValidated -= TarkovTrackerService_TokenValidated;
                        tarkovTrackerService.TokenInvalid -= TarkovTrackerService_TokenInvalid;
                        tarkovTrackerService.ProgressRetrieved -= TarkovTrackerService_ProgressRetrieved;

                        foreach (var moduleSetting in AppConfig.HideoutModuleSettings)
                        {
                            moduleSetting.PropertyChanged -= HideoutModuleSetting_PropertyChanged;
                        }

                        foreach (var moduleSetting in AppConfig.HideoutModuleSettingsPVE)
                        {
                            moduleSetting.PropertyChanged -= HideoutModuleSetting_PropertyChanged;
                        }
                    }

                    // Delete the config.json file if it exists
                    if (File.Exists(ConfigFilePath))
                    {
                        File.Delete(ConfigFilePath);
                        logger.Info("Configuration file deleted successfully.");
                    }

                    // Reset AppConfig to default settings
                    AppConfig = GetDefaultConfig();

                    // Update the DataContext to the new AppConfig
                    this.DataContext = AppConfig;

                    // Re-initialize UI elements that are not automatically bound
                    InitializeMonitorList();
                    InitializeStartingTabs();

                    // Refresh keybinds ListView
                    KeybindsListView.ItemsSource = null;
                    KeybindsListView.ItemsSource = AppConfig.Keybinds;

                    // Refresh other UI elements if necessary
                    SetProfileModeComboBoxSelection();
                    CraftingLevelDisplay.Text = $"Current Level: {AppConfig.CurrentCraftingLevel}";
                    CraftingLevelSlider.Value = AppConfig.CurrentCraftingLevel;
                    CraftingLevelDisplayPVE.Text = $"Current Level: {AppConfig.CurrentCraftingLevelPVE}";
                    CraftingLevelSliderPVE.Value = AppConfig.CurrentCraftingLevelPVE;

                    // Subscribe to PropertyChanged for the new AppConfig
                    AppConfig.PropertyChanged += AppConfig_PropertyChanged;
                    AppConfig.HideoutModuleSettings.CollectionChanged += HideoutModuleSettings_CollectionChanged;
                    AppConfig.CraftModuleSettings.CollectionChanged += CraftModuleSettings_CollectionChanged;
                    AppConfig.HideoutModuleSettingsPVE.CollectionChanged += HideoutModuleSettings_CollectionChanged;
                    AppConfig.CraftModuleSettingsPVE.CollectionChanged += CraftModuleSettings_CollectionChanged;
                    AppConfig.Keybinds.CollectionChanged += Keybinds_CollectionChanged;
                    tarkovTrackerService.TokenValidated += TarkovTrackerService_TokenValidated;
                    tarkovTrackerService.TokenInvalid += TarkovTrackerService_TokenInvalid;
                    tarkovTrackerService.ProgressRetrieved += TarkovTrackerService_ProgressRetrieved;

                    // Initialize Hideout Modules and Crafting Window List with default settings
                    MainWindow.UtilizeAndUpdateProfileMode(slider: false);

                    // Save the default config to create a new config.json
                    SaveConfig();

                    logger.Info("Configuration has been reset to default.");
                    MessageBox.Show("All configuration settings have been reset to their default values.", "Reset Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to reset configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    logger.Error(ex, "Failed to reset configuration.");
                }
            }
        }

        private void SetProfileModeComboBoxSelection()
        {
            switch (AppConfig.SelectedProfileMode)
            {
                case ProfileMode.Automatic:
                    ProfileModeComboBox.SelectedIndex = 0;
                    break;  
                case ProfileMode.Regular:
                    ProfileModeComboBox.SelectedIndex = 1;
                    break;
                case ProfileMode.Pve:
                    ProfileModeComboBox.SelectedIndex = 2;
                    break;
                default:
                    ProfileModeComboBox.SelectedIndex = 0;
                    break;
            }
        }

        private async void AppConfig_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            debounceDispatcher.Debounce(() => SaveConfig());
            if ((e.PropertyName == nameof(AppConfig.SelectedProfileMode) || e.PropertyName == nameof(AppConfig.EffectiveProfileMode)) && AppConfig.IsTarkovTrackerApiEnabled)
            {
                // Update the Tarkov Tracker Service token
                tarkovTrackerService.UpdateToken();
            }
        }

        private void HideoutModuleSettings_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Handle additions and removals
            if (e.NewItems != null)
            {
                foreach (HideoutModuleSetting newItem in e.NewItems)
                {
                    newItem.PropertyChanged += HideoutModuleSetting_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (HideoutModuleSetting oldItem in e.OldItems)
                {
                    oldItem.PropertyChanged -= HideoutModuleSetting_PropertyChanged;
                }
            }

            // Trigger a save
            debounceDispatcher.Debounce(() => SaveConfig());
        }

        private void CraftModuleSettings_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Handle additions/removals if needed, such as subscribing to PropertyChanged events
            if (e.NewItems != null)
            {
                foreach (CraftModuleSetting newItem in e.NewItems)
                {
                    newItem.PropertyChanged += CraftModuleSetting_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (CraftModuleSetting oldItem in e.OldItems)
                {
                    oldItem.PropertyChanged -= CraftModuleSetting_PropertyChanged;
                }
            }

            // Trigger a save
            debounceDispatcher.Debounce(() => SaveConfig());
        }

        private void CraftModuleSetting_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CraftModuleSetting.IsUnlocked))
            {
                // Debounce the save operation
                debounceDispatcher.Debounce(() => SaveConfig());
            }
        }

        private void Keybinds_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Handle additions
            if (e.NewItems != null)
            {
                foreach (KeybindEntry newItem in e.NewItems)
                {
                    newItem.PropertyChanged += KeybindEntry_PropertyChanged;
                }
            }

            // Handle removals
            if (e.OldItems != null)
            {
                foreach (KeybindEntry oldItem in e.OldItems)
                {
                    oldItem.PropertyChanged -= KeybindEntry_PropertyChanged;
                }
            }

            // Trigger save or other actions
            debounceDispatcher.Debounce(() => SaveConfig());
        }

        private void KeybindEntry_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(KeybindEntry.Keybind) || e.PropertyName == nameof(KeybindEntry.Functionality))
            {
                var entry = sender as KeybindEntry;
                if (entry == null) return;

                // Check for duplicates
                var duplicates = AppConfig.Keybinds
                    .Where(k => k != entry && k.Keybind == entry.Keybind)
                    .ToList();

                if (duplicates.Any())
                {
                    // Inform user
                    MessageBox.Show($"The keybind '{entry.Keybind}' is already in use by '{duplicates.First().Functionality}'. Please choose a different keybind.",
                        "Duplicate Keybind", MessageBoxButton.OK, MessageBoxImage.Warning);

                    // Revert the keybind or clear it
                    entry.Keybind = string.Empty;
                    return;
                }

                // If no duplicates, proceed normally
                debounceDispatcher.Debounce(() =>
                {
                    SaveConfig();
                });
            }
        }


        private void KeybindTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var textBox = (TextBox)sender;
            var entry = textBox.DataContext as KeybindEntry;
            if (entry == null) return;

            textBox.Text = string.Empty;

            // Determine modifiers
            var modifiers = new List<string>();
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) modifiers.Add("Ctrl");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) modifiers.Add("Shift");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) modifiers.Add("Alt");

            Key actualKey = e.Key;

            // If key is 'System', use SystemKey to get the actual key pressed (often for Alt combos)
            if (actualKey == Key.System)
            {
                actualKey = e.SystemKey;
            }

            // Ignore pure modifier keys
            if (actualKey == Key.LeftCtrl || actualKey == Key.RightCtrl ||
                actualKey == Key.LeftShift || actualKey == Key.RightShift ||
                actualKey == Key.LeftAlt || actualKey == Key.RightAlt ||
                actualKey == Key.Tab) // Example of ignoring Tab as well
            {
                // Wait for another key
                return;
            }

            string mainKey = actualKey.ToString();

            // Handle D1, D2, etc. by removing leading 'D'
            if (mainKey.StartsWith("D") && mainKey.Length == 2 && char.IsDigit(mainKey[1]))
            {
                mainKey = mainKey.Substring(1); // "D1" -> "1"
            }

            // Now build the final keybind string
            string keybind = modifiers.Count > 0
                ? string.Join("+", modifiers) + "+" + mainKey
                : mainKey;

            entry.Keybind = keybind;

            e.Handled = true;
        }

        private void SaveChangesButton_Click(object sender, RoutedEventArgs e)
        {
            SaveConfig();
            MainWindow?.RegisterConfiguredHotKeys();
            MessageBox.Show("Changes saved and hotkeys updated.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void KeybindTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            MainWindow?.DisableHotkeysTemporarily();
        }

        private void KeybindTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // If user clicks away without finishing, just re-enable hotkeys
            MainWindow?.ReenableHotkeys();
        }

        private async void ValidateApiTokenAsync()
        {
            bool isValid = await tarkovTrackerService.ValidateTokenAsync();
            if (isValid)
            {
                // Proceed with fetching data
                var hideoutLevels = await tarkovTrackerService.GetHideoutModuleLevelsAsync();
                var finishedQuests = await tarkovTrackerService.GetFinishedQuestsAsync();

                // TODO: Process and bind the data to your UI
            }
            else
            {
                // Inform the user to configure the API key
                MessageBox.Show("Invalid or missing Tarkov Tracker API token. Please configure it in the settings.", "API Token Invalid", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void TarkovTrackerService_ProgressRetrieved(object sender, EventArgs e)
        {
            // Handle progress retrieval events
            // For example, update UI elements or notify other components
        }

        private void TarkovTrackerService_TokenInvalid(object sender, EventArgs e)
        {
            // Handle invalid token events
            MessageBox.Show("Your Tarkov Tracker API token is invalid. Please update it in the settings.", "Invalid Token", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void TarkovTrackerService_TokenValidated(object sender, EventArgs e)
        {
            // Handle successful token validation
            logger.Info("Tarkov Tracker API token validated successfully.");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
