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

namespace EFT_OverlayAPP
{
    public partial class ConfigWindow : Window
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private const string ConfigFilePath = "config.json";
        public ObservableCollection<KeybindEntry> Keybinds { get; set; }
        public AppConfig AppConfig { get; set; }
        private DebounceDispatcher debounceDispatcher = new DebounceDispatcher(1000); // 1 second debounce
        public ConfigWindow()
        {
            InitializeComponent();
            LoadConfig();
            this.DataContext = AppConfig;
            InitializeMonitorList();
            InitializeStartingTabs();
            this.Loaded += ConfigWindow_Loaded; // Subscribe to Loaded event
        }

        private async void ConfigWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadHideoutModulesAsync();
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
        }

        private AppConfig GetDefaultConfig()
        {
            return new AppConfig
            {
                Keybinds = new List<KeybindEntry>
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
                HideoutModuleSettings = new ObservableCollection<HideoutModuleSetting>()
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

            // Validate API Keys if enabled
            if (AppConfig.IsTarkovTrackerApiEnabled)
            {
                if (string.IsNullOrWhiteSpace(AppConfig.PvpApiKey) || string.IsNullOrWhiteSpace(AppConfig.PveApiKey))
                {
                    MessageBox.Show("Both PVP and PVE API keys must be provided when Tarkov Tracker API is enabled.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            // Implement logic to retrieve the default EFT Logs path
            // This could be from AppConfig or a predefined location
            // For example:
            return @"C:\Battlestate Games\EFT\Logs"; // Replace with actual default path
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
            CraftingStartingTabComboBox.Items.Add("Tab 1");
            CraftingStartingTabComboBox.Items.Add("Tab 2");
            CraftingStartingTabComboBox.Items.Add("Tab 3");
            CraftingStartingTabComboBox.SelectedIndex = 0;

            // Example for RequiredItemsStartingTabComboBox
            RequiredItemsStartingTabComboBox.Items.Clear();
            RequiredItemsStartingTabComboBox.Items.Add("Tab A");
            RequiredItemsStartingTabComboBox.Items.Add("Tab B");
            RequiredItemsStartingTabComboBox.Items.Add("Tab C");
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

                // Update CurrentProfileModeTextBlock
                CurrentProfileModeTextBlock.Text = selectedContent;

                // Optionally, trigger changes based on profile mode
            }
        }

        // Event Handler for Crafting Level Slider
        private void CraftingLevelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CraftingLevelDisplay != null)
            {
                int level = (int)e.NewValue;
                CraftingLevelDisplay.Text = $"Current Level: {level}";
            }
        }

        // Event Handlers for Overlay Tab
        private void ApplyOverlayChangesButton_Click(object sender, RoutedEventArgs e)
        {
            // Since data binding handles updating AppConfig, simply save the configuration
            SaveConfig();
            MessageBox.Show("Overlay settings applied successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Event Handlers for Minimap Tab
        private void ApplyMinimapChangesButton_Click(object sender, RoutedEventArgs e)
        {
            SaveConfig();
            MessageBox.Show("Minimap settings applied successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Event Handlers for Crafting Tab
        private void ApplyCraftingChangesButton_Click(object sender, RoutedEventArgs e)
        {
            SaveConfig();
            MessageBox.Show("Crafting settings applied successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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

        // Event Handlers for Hideout Tab
        private void HideoutSourceRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (ManualHideoutSourceRadioButton == null)
            {
                MessageBox.Show("ManualHideoutSourceRadioButton is null.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (HideoutModulesListView == null)
            {
                MessageBox.Show("HideoutModulesListView is null.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Enable or disable Hideout Modules settings based on source selection
            if (ManualHideoutSourceRadioButton.IsChecked == true)
            {
                HideoutModulesListView.IsEnabled = true;
                // Additional UI adjustments if needed
            }
            else
            {
                HideoutModulesListView.IsEnabled = false;
                // Additional UI adjustments if needed
            }
        }

        private async Task LoadHideoutModulesAsync()
        {
            await DataCache.LoadRequiredItemsData();

            if (DataCache.HideoutStations == null || !DataCache.HideoutStations.Any())
            {
                MessageBox.Show("No hideout stations data available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                logger.Error("Hideout stations data is empty.");
                return;
            }

            // Preserve existing settings by using a dictionary
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
                    // Validate the selected level exists for the station or is 0
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
                    SelectedLevel = selectedLevel
                };

                // Populate AvailableLevels
                foreach (var lvl in availableLevels)
                {
                    moduleSetting.AvailableLevels.Add(lvl);
                }

                AppConfig.HideoutModuleSettings.Add(moduleSetting);
            }

            // Bind the collection to the ListView
            HideoutModulesListView.ItemsSource = AppConfig.HideoutModuleSettings;
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

        // Event Handler for Refresh Hideout Modules Button
        private async void RefreshHideoutModulesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await DataCache.LoadRequiredItemsData();

                if (DataCache.HideoutStations == null || !DataCache.HideoutStations.Any())
                {
                    MessageBox.Show("No hideout stations data available to refresh.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    logger.Error("Hideout stations data is empty on refresh.");
                    return;
                }

                // Preserve existing settings
                var existingSettingsDict = AppConfig.HideoutModuleSettings.ToDictionary(h => h.ModuleName, h => h.SelectedLevel);

                // Clear and reload settings
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
                        SelectedLevel = selectedLevel
                    };

                    // Populate AvailableLevels
                    foreach (var lvl in availableLevels)
                    {
                        moduleSetting.AvailableLevels.Add(lvl);
                    }

                    // Subscribe to PropertyChanged event for automatic saving
                    moduleSetting.PropertyChanged += HideoutModuleSetting_PropertyChanged;

                    AppConfig.HideoutModuleSettings.Add(moduleSetting);
                }

                // Refresh the ListView binding
                HideoutModulesListView.ItemsSource = null;
                HideoutModulesListView.ItemsSource = AppConfig.HideoutModuleSettings;

                MessageBox.Show("Hideout modules have been refreshed successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                logger.Info("Hideout modules refreshed successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to refresh hideout modules: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                logger.Error(ex, "Failed to refresh hideout modules.");
            }
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

        // Event Handlers for Paths Settings Tab (within General)
        private void BrowseEftLogsPathButton_Click(object sender, RoutedEventArgs e)
        {
            // Open folder browser dialog to select EFT Logs path
            var dialog = new VistaFolderBrowserDialog
            {
                Description = "Select the EFT Logs directory.",
                UseDescriptionForTitle = true
            };

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                string selectedPath = dialog.SelectedPath;

                // Validate the selected path
                if (Directory.Exists(selectedPath))
                {
                    EftLogsPathTextBox.Text = selectedPath;

                    // Update AppConfig with the new path if custom path is enabled
                    if (AppConfig.UseCustomEftLogsPath)
                    {
                        AppConfig.EftLogsPath = selectedPath;
                        SaveConfig();
                    }
                }
                else
                {
                    MessageBox.Show("The selected path does not exist. Please choose a valid directory.", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Warning);
                    logger.Warn($"Invalid EFT Logs path selected: {selectedPath}");
                }
            }
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

                logger.Info("Keybinds have been reset to default.");
                MessageBox.Show("Keybinds have been reset to their default values.",
                                "Reset Successful",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
            }
        }

        // Helper method to get default keybinds
        private List<KeybindEntry> GetDefaultKeybinds()
        {
            return new List<KeybindEntry>
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

        private void CraftSourceRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (ManualCraftSourceRadioButton == null || TarkovTrackerCraftSourceRadioButton == null || UnlockableCraftsListView == null)
            {
                MessageBox.Show("One or more controls are not initialized properly.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (ManualCraftSourceRadioButton.IsChecked == true)
            {
                UnlockableCraftsListView.IsEnabled = true;
                // Additional logic to handle manual selection can be added here
                logger.Info("Craft source set to Manual.");
            }
            else if (TarkovTrackerCraftSourceRadioButton.IsChecked == true)
            {
                UnlockableCraftsListView.IsEnabled = false;
                // Logic to load crafts from Tarkov Tracker API can be implemented here
                logger.Info("Craft source set to Tarkov Tracker.");
            }
        }
    }
}
