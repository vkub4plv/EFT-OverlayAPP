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

namespace EFT_OverlayAPP
{
    /// <summary>
    /// Interaction logic for ConfigWindow.xaml
    /// </summary>
    public partial class ConfigWindow : Window
    {
        public ObservableCollection<KeybindEntry> Keybinds { get; set; }
        public ConfigWindow()
        {
            InitializeComponent();
            InitializeKeybinds();
            InitializeMonitorList();
            InitializeStartingTabs();
            this.DataContext = this; // Set DataContext for data binding
        }

        #region Initialization Methods

        private void InitializeKeybinds()
        {
            // Initialize Keybinds with default values
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
            };

            KeybindsListView.ItemsSource = Keybinds;
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

        #endregion

        #region Event Handlers

        // Keybinds Tab Events
        private void ResetKeybindsButton_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder: Reset keybinds to default values
            InitializeKeybinds();
        }

        // Map Settings Tab Events
        private void EnableTarkovTrackerApiCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            TarkovTrackerApiTextBox.IsEnabled = true;
        }

        private void EnableTarkovTrackerApiCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            TarkovTrackerApiTextBox.IsEnabled = false;
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
            // Placeholder: Open folder browser dialog to select EFT Logs path
            MessageBox.Show("Folder browser dialog opened.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UseCustomEftLogsPathCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            EftLogsPathTextBox.IsReadOnly = false;
        }

        private void UseCustomEftLogsPathCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            EftLogsPathTextBox.IsReadOnly = true;
            // Reset to default path if necessary
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

        #endregion
    }

    public class KeybindEntry
    {
        public string Functionality { get; set; }
        public string Keybind { get; set; }
    }
}
