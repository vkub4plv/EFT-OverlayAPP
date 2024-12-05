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

namespace EFT_OverlayAPP
{
    public partial class ConfigWindow : Window
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private const string ConfigFilePath = "config.json";
        public ObservableCollection<KeybindEntry> Keybinds { get; set; }
        public AppConfig AppConfig { get; set; }
        public ConfigWindow()
        {
            InitializeComponent();
            LoadConfig();
            this.DataContext = this; // Set DataContext for data binding
            InitializeMonitorList();
            InitializeStartingTabs();
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

            // Enable Tarkov Tracker API Checkbox
            EnableTarkovTrackerApiCheckBox.IsChecked = AppConfig.IsTarkovTrackerApiEnabled;

            // PVP API Key
            PvpApiKeyTextBox.Text = AppConfig.PvpApiKey;

            // PVE API Key
            PveApiKeyTextBox.Text = AppConfig.PveApiKey;

            // Selected Map
            if (!string.IsNullOrEmpty(AppConfig.SelectedMap))
            {
                foreach (var item in MapSelectionComboBox.Items)
                {
                    if ((item as ComboBoxItem)?.Content.ToString() == AppConfig.SelectedMap)
                    {
                        MapSelectionComboBox.SelectedItem = item;
                        break;
                    }
                }
            }

            // Toggle Visibilities
            ToggleMinimapVisibilityCheckBox.IsChecked = AppConfig.ToggleMinimapVisibility;
            ToggleRaidTimerVisibilityCheckBox.IsChecked = AppConfig.ToggleRaidTimerVisibility;
            ToggleCraftingTimersVisibilityCheckBox.IsChecked = AppConfig.ToggleCraftingTimersVisibility;
            ToggleOtherWindowButtonsCheckBox.IsChecked = AppConfig.ToggleOtherWindowButtons;

            // New Settings

            // Crafting Level
            if (AppConfig.CurrentCraftingLevel < 0 || AppConfig.CurrentCraftingLevel > 51)
            {
                AppConfig.CurrentCraftingLevel = 0; // Reset to default if out of range
            }
            CraftingLevelSlider.Value = AppConfig.CurrentCraftingLevel;
            CraftingLevelDisplay.Text = $"Current Level: {AppConfig.CurrentCraftingLevel}";

            // Disable Auto-Hide Raid Timer
            DisableAutoHideRaidTimerCheckBox.IsChecked = AppConfig.DisableAutoHideRaidTimer;

            // Initialize Paths Settings
            InitializePathsSettings();

            // Set Profile Mode ComboBox
            SetProfileModeComboBoxSelection();
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
                SelectedMap = "Map Genie",
                ToggleMinimapVisibility = false,
                ToggleRaidTimerVisibility = false,
                ToggleCraftingTimersVisibility = false,
                ToggleOtherWindowButtons = false,
                CurrentCraftingLevel = 0,
                DisableAutoHideRaidTimer = false,
                UseCustomEftLogsPath = false,
                EftLogsPath = GetDefaultEftLogsPath(),
                SelectedProfileMode = ProfileMode.Automatic // Default to Automatic
                // Initialize other settings as needed
            };
        }

        private void SaveConfig()
        {
            // Update AppConfig with current UI settings

            // Validate crafting level
            int craftingLevel = (int)CraftingLevelSlider.Value;
            if (craftingLevel < 0 || craftingLevel > 51)
            {
                MessageBox.Show("Crafting level must be between 0 and 51.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Keybinds
            AppConfig.Keybinds = KeybindsListView.ItemsSource as List<KeybindEntry>;

            // PVP API Key
            AppConfig.PvpApiKey = PvpApiKeyTextBox.Text;

            // PVE API Key
            AppConfig.PveApiKey = PveApiKeyTextBox.Text;

            // Enable Tarkov Tracker API
            AppConfig.IsTarkovTrackerApiEnabled = EnableTarkovTrackerApiCheckBox.IsChecked == true;

            // Selected Map
            AppConfig.SelectedMap = (MapSelectionComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

            // Toggle Visibilities
            AppConfig.ToggleMinimapVisibility = ToggleMinimapVisibilityCheckBox.IsChecked == true;
            AppConfig.ToggleRaidTimerVisibility = ToggleRaidTimerVisibilityCheckBox.IsChecked == true;
            AppConfig.ToggleCraftingTimersVisibility = ToggleCraftingTimersVisibilityCheckBox.IsChecked == true;
            AppConfig.ToggleOtherWindowButtons = ToggleOtherWindowButtonsCheckBox.IsChecked == true;

            // New Settings

            // Crafting Level
            AppConfig.CurrentCraftingLevel = (int)CraftingLevelSlider.Value;

            // Disable Auto-Hide Raid Timer
            AppConfig.DisableAutoHideRaidTimer = DisableAutoHideRaidTimerCheckBox.IsChecked == true;

            // Paths Settings
            if (UseCustomEftLogsPathCheckBox.IsChecked == true)
            {
                AppConfig.EftLogsPath = EftLogsPathTextBox.Text;
                AppConfig.UseCustomEftLogsPath = true;
            }
            else
            {
                AppConfig.EftLogsPath = GetDefaultEftLogsPath();
                AppConfig.UseCustomEftLogsPath = false;
            }

            try
            {
                string json = JsonConvert.SerializeObject(AppConfig, Formatting.Indented);
                File.WriteAllText(ConfigFilePath, json);
                logger.Info("Configuration saved successfully.");
                MessageBox.Show("Configuration saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                logger.Error(ex, "Failed to save configuration.");
            }
        }

        private void InitializePathsSettings()
        {
            // Set the checkbox state
            UseCustomEftLogsPathCheckBox.IsChecked = AppConfig.UseCustomEftLogsPath;

            // Set the TextBox state
            EftLogsPathTextBox.IsEnabled = AppConfig.UseCustomEftLogsPath;
            EftLogsPathTextBox.Text = AppConfig.UseCustomEftLogsPath ? AppConfig.EftLogsPath : GetDefaultEftLogsPath();
        }

        private string GetDefaultEftLogsPath()
        {
            // Implement logic to retrieve the default EFT Logs path
            // This could be from AppConfig or a predefined location
            // For example:
            return @"C:\Battlestate Games\EFT\Logs"; // Replace with actual default path
        }

        private void SetProfileModeComboBoxSelection()
        {
            switch (AppConfig.SelectedProfileMode)
            {
                case ProfileMode.Automatic:
                    ProfileModeComboBox.SelectedIndex = 0; // Automatic
                    break;
                case ProfileMode.Regular:
                    ProfileModeComboBox.SelectedIndex = 1; // Regular (PVP)
                    break;
                case ProfileMode.Pve:
                    ProfileModeComboBox.SelectedIndex = 2; // PVE
                    break;
                default:
                    ProfileModeComboBox.SelectedIndex = 0; // Default to Automatic
                    break;
            }
        }

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

                // Update UI or application behavior based on the new profile mode
                // For example, enable/disable automatic profile switching
            }
        }


        // Crafting Level Slider Value Changed
        private void CraftingLevelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CraftingLevelDisplay != null)
            {
                int level = (int)e.NewValue;
                CraftingLevelDisplay.Text = $"Current Level: {level}";
            }
        }

        // Disable Auto-Hide Raid Timer CheckBox Checked
        private void DisableAutoHideRaidTimerCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // Additional logic if needed when checked
        }

        // Disable Auto-Hide Raid Timer CheckBox Unchecked
        private void DisableAutoHideRaidTimerCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // Additional logic if needed when unchecked
        }

        // Reset Keybinds Button
        private void ResetKeybindsButton_Click(object sender, RoutedEventArgs e)
        {
            // Reset keybinds to default values
            AppConfig.Keybinds = GetDefaultConfig().Keybinds;
            KeybindsListView.ItemsSource = AppConfig.Keybinds;
        }

        // Toggle Raid Timer Visibility CheckBox Checked
        private void ToggleRaidTimerVisibilityCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            AppConfig.ToggleRaidTimerVisibility = true;
            logger.Info("Raid Timer visibility enabled.");
            // Add logic to show the raid timer in the application
        }

        // Toggle Raid Timer Visibility CheckBox Unchecked
        private void ToggleRaidTimerVisibilityCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AppConfig.ToggleRaidTimerVisibility = false;
            logger.Info("Raid Timer visibility disabled.");
            // Add logic to hide the raid timer in the application
        }

        // Toggle Crafting Timers Visibility CheckBox Checked
        private void ToggleCraftingTimersVisibilityCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            AppConfig.ToggleCraftingTimersVisibility = true;
            logger.Info("Crafting Timers visibility enabled.");
            // Add logic to show crafting timers in the application
        }

        // Toggle Crafting Timers Visibility CheckBox Unchecked
        private void ToggleCraftingTimersVisibilityCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AppConfig.ToggleCraftingTimersVisibility = false;
            logger.Info("Crafting Timers visibility disabled.");
            // Add logic to hide crafting timers in the application
        }

        // Toggle Other Window Buttons CheckBox Checked
        private void ToggleOtherWindowButtonsCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            AppConfig.ToggleOtherWindowButtons = true;
            logger.Info("Other Window Buttons visibility enabled.");
            // Add logic to show other window buttons in the application
        }

        // Toggle Other Window Buttons CheckBox Unchecked
        private void ToggleOtherWindowButtonsCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AppConfig.ToggleOtherWindowButtons = false;
            logger.Info("Other Window Buttons visibility disabled.");
            // Add logic to hide other window buttons in the application
        }

        // Enable Tarkov Tracker API CheckBox Checked
        private void EnableTarkovTrackerApiCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            PvpApiKeyTextBox.IsEnabled = true;
            PveApiKeyTextBox.IsEnabled = true;
        }

        // Enable Tarkov Tracker API CheckBox Unchecked
        private void EnableTarkovTrackerApiCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            PvpApiKeyTextBox.IsEnabled = false;
            PveApiKeyTextBox.IsEnabled = false;
            // Optionally, clear the API keys or retain them
            // PvpApiKeyTextBox.Text = string.Empty;
            // PveApiKeyTextBox.Text = string.Empty;
        }

        // Hideout Modules Tab Events
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

            // Placeholder: Enable or disable Hideout Modules settings based on source selection
            if (ManualHideoutSourceRadioButton.IsChecked == true)
            {
                HideoutModulesListView.IsEnabled = true;
                // Additional UI adjustments
            }
            else
            {
                HideoutModulesListView.IsEnabled = false;
                // Additional UI adjustments
            }
        }

        private void RefreshHideoutModulesButton_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder: Refresh the list of hideout modules
            // This will be implemented with actual data retrieval logic
            MessageBox.Show("Hideout Modules refreshed.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Crafting Settings Tab Events
        private void FilterBasedOnHideoutLevelsCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // Placeholder: Enable filtering based on hideout levels
        }

        private void FilterBasedOnHideoutLevelsCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // Placeholder: Disable filtering based on hideout levels
        }

        private void ShowUnlockedQuestRecipesCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // Placeholder: Enable showing unlocked quest-related recipes
        }

        private void ShowUnlockedQuestRecipesCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // Placeholder: Disable showing unlocked quest-related recipes
        }

        private void CraftSourceRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (ManualCraftSourceRadioButton == null)
            {
                MessageBox.Show("ManualCraftSourceRadioButton is null.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (UnlockableCraftsListView == null)
            {
                MessageBox.Show("UnlockableCraftsListView is null.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Placeholder: Toggle between manual and Tarkov Tracker sources for crafts
            if (ManualCraftSourceRadioButton.IsChecked == true)
            {
                UnlockableCraftsListView.IsEnabled = true;
                // Enable manual selection
            }
            else
            {
                UnlockableCraftsListView.IsEnabled = false;
                // Display crafts from Tarkov Tracker
            }
        }

        // Display Settings Tab Events
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

        // Paths Settings Tab Events
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
                    if (UseCustomEftLogsPathCheckBox.IsChecked == true)
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

        // Monitor Settings Tab Events
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

        private void InitializeMonitorList()
        {
            // Placeholder: Populate MonitorSelectionComboBox with detected monitors
            // This will be implemented with actual monitor detection logic
            MonitorSelectionComboBox.Items.Add("Monitor 1");
            MonitorSelectionComboBox.Items.Add("Monitor 2");
            MonitorSelectionComboBox.Items.Add("Monitor 3");
            // Set default selection
            MonitorSelectionComboBox.SelectedIndex = 0;
            CurrentMonitorTextBlock.Text = MonitorSelectionComboBox.SelectedItem as string;
        }

        private void InitializeStartingTabs()
        {
            // Placeholder: Populate CraftingStartingTabComboBox and RequiredItemsStartingTabComboBox
            // These should reflect the actual tabs available in Crafting and Required Items windows

            // Example for CraftingStartingTabComboBox
            CraftingStartingTabComboBox.Items.Add("Tab 1");
            CraftingStartingTabComboBox.Items.Add("Tab 2");
            CraftingStartingTabComboBox.Items.Add("Tab 3");
            CraftingStartingTabComboBox.SelectedIndex = 0;

            // Example for RequiredItemsStartingTabComboBox
            RequiredItemsStartingTabComboBox.Items.Add("Tab A");
            RequiredItemsStartingTabComboBox.Items.Add("Tab B");
            RequiredItemsStartingTabComboBox.Items.Add("Tab C");
            RequiredItemsStartingTabComboBox.SelectedIndex = 0;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            SaveConfig();
        }
    }
}
