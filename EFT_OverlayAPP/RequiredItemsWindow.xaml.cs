using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
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
using static System.Collections.Specialized.BitVector32;

namespace EFT_OverlayAPP
{
    public partial class RequiredItemsWindow : Window, INotifyPropertyChanged
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private Dictionary<string, CombinedRequiredItemEntry> combinedItemDictionary = new Dictionary<string, CombinedRequiredItemEntry>();
        public ObservableCollection<RequiredItemEntry> RequiredItems { get; set; } = new ObservableCollection<RequiredItemEntry>();
        public ICollectionView RequiredItemsView;
        public ConfigWindow ConfigWindow { get; set; }
        private bool loadedAsPVE = false;
        private bool loadedManualAsPVE = false;
        private bool isRequiredDataLoading;
        public bool IsRequiredDataLoading
        {
            get => isRequiredDataLoading;
            set
            {
                isRequiredDataLoading = value;
                OnPropertyChanged(nameof(IsRequiredDataLoading));
            }
        }

        MainWindow MainWindow { get; set; }

        public RequiredItemsWindow(MainWindow mainWindow, ConfigWindow configWindow)
        {
            InitializeComponent();
            DataContext = this;
            ConfigWindow = configWindow;
            MainWindow = mainWindow;

            // Start data initialization
            InitializeData();

            // Set the starting tab
            SelectStartingTab(ConfigWindow.AppConfig.RequiredItemsStartingTab);

            ConfigWindow.AppConfig.PropertyChanged += AppConfig_PropertyChanged;
        }

        private async void InitializeData()
        {
            // Show loading indicator
            IsRequiredDataLoading = true;

            // Load data from DataCache
            await DataCache.LoadRequiredItemsData();

            // Check if data has already been loaded to avoid re-processing
            if (RequiredItems.Count == 0)
            {
                // Process data into RequiredItems
                LoadRequiredItems();

                // Setup CollectionView
                RequiredItemsView = CollectionViewSource.GetDefaultView(RequiredItems);
                RequiredItemsView.GroupDescriptions.Add(new PropertyGroupDescription("GroupType"));
                RequiredItemsView.Filter = RequiredItemsFilter;
                RequiredItemsListView.ItemsSource = RequiredItemsView;

                // Load combined items
                LoadCombinedRequiredItems();

                // Load manual combined items
                LoadManualCombinedRequiredItems();

                // Populate filters
                PopulateFilters();

                // Load quantities from file
                LoadQuantities();

                // Apply initial sorting
                ApplyRequiredItemsSorting();
                ApplyCombinedRequiredItemsSorting();
                ApplyManualCombinedRequiredItemsSorting();
            }
            else
            {
                // Data already loaded, refresh views
                RequiredItemsView?.Refresh();
                CombinedRequiredItemsView?.Refresh();
                ManualCombinedRequiredItemsView?.Refresh();
            }

            // Hide loading indicator
            IsRequiredDataLoading = false;
        }

        private void LoadRequiredItems()
        {
            // Load Quest Items
            foreach (var quest in DataCache.Quests)
            {
                foreach (var objective in quest.Objectives)
                {
                    // Only process objectives with items
                    if (objective.Items != null && objective.Items.Any())
                    {
                        if (objective.Items.Count > 1)
                        {
                            // Multiple items can fulfill the objective
                            var combinedEntry = new RequiredItemEntry
                            {
                                Id = objective.Id,
                                Item = new Item
                                {
                                    Name = $"{quest.Name} - Multiple Items",
                                    IconLink = quest.Trader.ImageLink
                                },
                                QuantityNeeded = objective.Count,
                                QuantityOwned = 0,
                                IsFoundInRaid = objective.FoundInRaid,
                                SourceIcon = quest.Trader.ImageLink,
                                SourceName = quest.Trader.Name,
                                SourceDetail = quest.Name,
                                GroupType = "Quests",
                                IsCombined = true,
                                ChildEntries = new List<RequiredItemEntry>()
                            };

                            foreach (var item in objective.Items)
                            {
                                var entry = new RequiredItemEntry
                                {
                                    Id = item.Id,
                                    Item = item,
                                    QuantityNeeded = objective.Count,
                                    QuantityOwned = 0,
                                    IsFoundInRaid = objective.FoundInRaid,
                                    SourceIcon = quest.Trader.ImageLink,
                                    SourceName = quest.Trader.Name,
                                    SourceDetail = quest.Name,
                                    GroupType = "Quests",
                                    ParentEntry = combinedEntry
                                };
                                combinedEntry.ChildEntries.Add(entry);
                                RequiredItems.Add(entry);
                            }

                            RequiredItems.Add(combinedEntry);
                        }
                        else
                        {
                            // Single item
                            var item = objective.Items.First();
                            var entry = new RequiredItemEntry
                            {
                                Id = item.Id,
                                Item = item,
                                QuantityNeeded = objective.Count,
                                QuantityOwned = 0,
                                IsFoundInRaid = objective.FoundInRaid,
                                SourceIcon = quest.Trader.ImageLink,
                                SourceName = quest.Trader.Name,
                                SourceDetail = quest.Name,
                                GroupType = "Quests"
                            };
                            RequiredItems.Add(entry);
                        }
                    }
                }
            }

            // Load Hideout Items
            foreach (var station in DataCache.HideoutStations)
            {
                foreach (var level in station.Levels)
                {
                    foreach (var req in level.ItemRequirements)
                    {
                        var entry = new RequiredItemEntry
                        {
                            Id = $"{station.Id}_{level.Level}_{req.Item.Id}",
                            Item = req.Item,
                            QuantityNeeded = req.Count,
                            QuantityOwned = 0,
                            IsFoundInRaid = false,
                            SourceIcon = station.ImageLink,
                            SourceName = station.Name,
                            SourceDetail = $"{station.Name} Level {level.Level}",
                            GroupType = "Hideout"
                        };
                        RequiredItems.Add(entry);
                    }
                }
            }
        }

        private void PopulateFilters()
        {
            // Type Filter
            TypeFilterComboBox.Items.Clear();
            TypeFilterComboBox.Items.Add("All");
            TypeFilterComboBox.Items.Add("Quests");
            TypeFilterComboBox.Items.Add("Hideout");
            TypeFilterComboBox.SelectedIndex = 0;

            // Completion Filter
            CompletionFilterComboBox.Items.Clear();
            CompletionFilterComboBox.Items.Add("All");
            CompletionFilterComboBox.Items.Add("Remaining Items");
            CompletionFilterComboBox.Items.Add("Completed Items");
            CompletionFilterComboBox.SelectedIndex = 0;

            // Sort Order
            SortOrderComboBox.Items.Clear();
            SortOrderComboBox.Items.Add("Default Order");
            SortOrderComboBox.Items.Add("Name A-Z");
            SortOrderComboBox.Items.Add("Name Z-A");
            SortOrderComboBox.SelectedIndex = 0;
        }

        private bool RequiredItemsFilter(object obj)
        {
            var entry = obj as RequiredItemEntry;
            if (entry == null) return false;

            // Filter by search text
            string searchText = SearchTextBox.Text?.ToLower() ?? string.Empty;
            if (!string.IsNullOrEmpty(searchText))
            {
                if (!entry.Item.Name.ToLower().Contains(searchText))
                    return false;
            }

            // Filter by type
            string selectedType = TypeFilterComboBox.SelectedItem as string ?? "All";
            if (selectedType != "All" && entry.GroupType != selectedType)
                return false;

            // Filter by completion status
            string selectedCompletion = CompletionFilterComboBox.SelectedItem as string ?? "All";
            if (selectedCompletion == "Remaining Items" && entry.IsComplete)
                return false;
            if (selectedCompletion == "Completed Items" && !entry.IsComplete)
                return false;

            // Filter by completed stations if config option if selected
            if (ConfigWindow.AppConfig.HideItemsForBuiltStations && entry.GroupType == "Hideout")
            {
                // Extract the module name and level from SourceDetail
                string moduleName = entry.SourceDetail.Split(" Level")[0];
                int level = int.Parse(entry.SourceDetail.Split("Level")[1].Trim());

                // Find the module with a matching name
                var matchingModule = ConfigWindow.AppConfig.EffectiveHideoutModuleSettings
                    .FirstOrDefault(module => module.ModuleName == moduleName);

                // Check if the module exists and its SelectedLevel is >= the extracted level
                if (matchingModule != null && matchingModule.SelectedLevel >= level)
                {
                    return false;
                }
            }

            // Filter by plant items (WI-FI Camera and MS2000 Marker) if config option is selected
            if (ConfigWindow.AppConfig.HidePlantItems)
            {
                if (entry.Item.Name == "WI-FI Camera" || entry.Item.Name == "MS2000 Marker")
                {
                    return false;
                }
            }

            return true;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RequiredItemsView?.Refresh();
        }

        private void TypeFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RequiredItemsView?.Refresh();
        }

        private void CompletionFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RequiredItemsView?.Refresh();
        }

        private void IncrementButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var entry = button.DataContext as RequiredItemEntry;
            if (entry != null)
            {
                if (entry.QuantityOwned < entry.QuantityNeeded)
                {
                    entry.QuantityOwned++;
                    if (entry.ParentEntry != null)
                    {
                        entry.ParentEntry.QuantityOwned = entry.ParentEntry.ChildEntries.Sum(c => c.QuantityOwned);
                    }
                    OnEntryUpdated(entry);

                    // Save quantities after change
                    SaveQuantities();
                }
            }
        }

        private void DecrementButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var entry = button.DataContext as RequiredItemEntry;
            if (entry != null)
            {
                if (entry.QuantityOwned > 0)
                {
                    entry.QuantityOwned--;
                    if (entry.ParentEntry != null)
                    {
                        entry.ParentEntry.QuantityOwned = entry.ParentEntry.ChildEntries.Sum(c => c.QuantityOwned);
                    }
                    OnEntryUpdated(entry);

                    // Save quantities after change
                    SaveQuantities();
                }
            }
        }

        private void OnEntryUpdated(RequiredItemEntry entry)
        {
            entry.OnPropertyChanged(nameof(entry.QuantityOwned));
            entry.OnPropertyChanged(nameof(entry.IsComplete));
            if (entry.ParentEntry != null)
            {
                entry.ParentEntry.QuantityOwned = entry.ParentEntry.ChildEntries.Sum(c => c.QuantityOwned);
                entry.ParentEntry.OnPropertyChanged(nameof(entry.ParentEntry.QuantityOwned));
                entry.ParentEntry.OnPropertyChanged(nameof(entry.ParentEntry.IsComplete));

                // Update IsComplete for all child entries
                foreach (var child in entry.ParentEntry.ChildEntries)
                {
                    child.OnPropertyChanged(nameof(child.IsComplete));
                }
            }
        }


        // Save and Load Quantities
        public void SaveQuantities()
        {
            try
            {
                var quantitiesData = RequiredItems.Select(e => new RequiredItemQuantity
                {
                    Id = e.Id,
                    QuantityOwned = e.QuantityOwned
                }).ToList();

                string json = JsonConvert.SerializeObject(quantitiesData, Formatting.Indented);
                string quantitiesFile = loadedAsPVE ? "quantities_pve.json" : "quantities.json";
                File.WriteAllText(quantitiesFile, json);
                logger.Info($"Quantities saved to '{quantitiesFile}'.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving quantities: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                logger.Error(ex, "Failed to save quantities.");
            }
        }

        private void LoadQuantities()
        {
            try
            {
                string quantitiesFile = App.IsPVEMode ? "quantities_pve.json" : "quantities.json";
                if (App.IsPVEMode)
                {
                    loadedAsPVE = true;
                }
                else
                {
                    loadedAsPVE = false;
                }

                if (File.Exists(quantitiesFile))
                {
                    string json = File.ReadAllText(quantitiesFile);
                    var quantitiesData = JsonConvert.DeserializeObject<List<RequiredItemQuantity>>(json);
                    foreach (var data in quantitiesData)
                    {
                        var entry = RequiredItems.FirstOrDefault(e => e.Id == data.Id);
                        if (entry != null)
                        {
                            entry.QuantityOwned = data.QuantityOwned;
                        }
                    }
                }
                else
                {
                    logger.Warn($"Quantities file '{quantitiesFile}' does not exist.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading quantities: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                logger.Error(ex, "Failed to load quantities.");
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true; // Cancel the close operation
            this.Hide(); // Hide the window instead
            SaveQuantities(); // Save quantities when hiding
            SaveManualCombinedQuantities(); // Save manual quantities when hiding
        }

        private void ApplyRequiredItemsSorting()
        {
            RequiredItemsView.SortDescriptions.Clear();

            string selectedSortOrder = SortOrderComboBox.SelectedItem as string ?? "Default Order";

            if (selectedSortOrder == "Name A-Z")
            {
                RequiredItemsView.SortDescriptions.Add(new SortDescription("Item.Name", ListSortDirection.Ascending));
            }
            else if (selectedSortOrder == "Name Z-A")
            {
                RequiredItemsView.SortDescriptions.Add(new SortDescription("Item.Name", ListSortDirection.Descending));
            }
            else
            {
                // Default order (as received from API)
                // No sorting applied
            }
        }

        private void SortOrderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyRequiredItemsSorting();
            RequiredItemsView?.Refresh();
        }

        public ObservableCollection<CombinedRequiredItemEntry> CombinedRequiredItems { get; set; } = new ObservableCollection<CombinedRequiredItemEntry>();
        public ICollectionView CombinedRequiredItemsView;

        private void LoadCombinedRequiredItems()
        {
            combinedItemDictionary.Clear();

            var groupedItems = RequiredItems
                .GroupBy(e => new { e.Item.Id, e.Item.Name, e.Item.IconLink, e.IsFoundInRaid })
                .Select(g =>
                {
                    var combinedEntry = new CombinedRequiredItemEntry
                    {
                        Item = new Item
                        {
                            Id = g.Key.Id,
                            Name = g.Key.Name,
                            IconLink = g.Key.IconLink
                        },
                        QuantityNeeded = g.Sum(e => e.QuantityNeeded),
                        QuantityOwned = g.Sum(e => e.QuantityOwned),
                        SourceEntries = g.ToList(),
                        IsFoundInRaid = g.Key.IsFoundInRaid,
                        RequiredForDetails = g.Select(e => new SourceDetail
                        {
                            Icon = e.SourceIcon,
                            Name = e.SourceDetail
                        }).Distinct().ToList()
                    };

                    // Map the combined entry to the required items
                    foreach (var entry in g)
                    {
                        entry.PropertyChanged -= RequiredItemEntry_PropertyChanged;
                        entry.PropertyChanged += RequiredItemEntry_PropertyChanged;
                        string key = $"{entry.Item.Id}_{entry.IsFoundInRaid}";
                        if (!combinedItemDictionary.ContainsKey(key))
                        {
                            combinedItemDictionary.Add(key, combinedEntry);
                        }
                    }

                    return combinedEntry;
                })
                .ToList();

            CombinedRequiredItems = new ObservableCollection<CombinedRequiredItemEntry>(groupedItems);

            // Setup CollectionView
            CombinedRequiredItemsView = CollectionViewSource.GetDefaultView(CombinedRequiredItems);
            CombinedRequiredItemsView.Filter = CombinedRequiredItemsFilter;
            CombinedRequiredItemsListView.ItemsSource = CombinedRequiredItemsView;

            // Populate filters
            PopulateCombinedFilters();

            // Apply initial sorting
            ApplyCombinedRequiredItemsSorting();
        }

        private bool IsSourceEntryVisible(RequiredItemEntry entry)
        {
            // Hide if station is built
            if (ConfigWindow.AppConfig.HideItemsForBuiltStations && entry.GroupType == "Hideout")
            {
                string moduleName = entry.SourceDetail.Split(" Level")[0];
                int level = int.Parse(entry.SourceDetail.Split("Level")[1].Trim());

                var matchingModule = ConfigWindow.AppConfig.EffectiveHideoutModuleSettings
                    .FirstOrDefault(module => module.ModuleName == moduleName);

                if (matchingModule != null && matchingModule.SelectedLevel >= level)
                {
                    return false;
                }
            }

            // Hide if plant items are to be hidden
            if (ConfigWindow.AppConfig.HidePlantItems)
            {
                if (entry.Item.Name == "WI-FI Camera" || entry.Item.Name == "MS2000 Marker")
                {
                    return false;
                }
            }

            return true;
        }


        private void RequiredItemEntry_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RequiredItemEntry.QuantityOwned))
            {
                var requiredEntry = sender as RequiredItemEntry;
                if (requiredEntry != null)
                {
                    // Find the corresponding combined entry
                    string key = $"{requiredEntry.Item.Id}_{requiredEntry.IsFoundInRaid}";
                    if (combinedItemDictionary.TryGetValue(key, out CombinedRequiredItemEntry combinedEntry))
                    {
                        // Update the combined entry's QuantityOwned
                        combinedEntry.QuantityOwned = RequiredItems
                            .Where(re => re.Item.Id == requiredEntry.Item.Id && re.IsFoundInRaid == requiredEntry.IsFoundInRaid)
                            .Sum(re => re.QuantityOwned);

                        // Notify property changed
                        combinedEntry.OnPropertyChanged(nameof(combinedEntry.QuantityOwned));
                        combinedEntry.OnPropertyChanged(nameof(combinedEntry.IsComplete));
                    }
                }
            }
        }


        private void PopulateCombinedFilters()
        {
            // Completion Filter
            CombinedCompletionFilterComboBox.Items.Clear();
            CombinedCompletionFilterComboBox.Items.Add("All");
            CombinedCompletionFilterComboBox.Items.Add("Remaining Items");
            CombinedCompletionFilterComboBox.Items.Add("Completed Items");
            CombinedCompletionFilterComboBox.SelectedIndex = 0;

            // Sort Order
            CombinedSortOrderComboBox.Items.Clear();
            CombinedSortOrderComboBox.Items.Add("Default Order");
            CombinedSortOrderComboBox.Items.Add("Name A-Z");
            CombinedSortOrderComboBox.Items.Add("Name Z-A");
            CombinedSortOrderComboBox.Items.Add("Quantity Needed Asc");
            CombinedSortOrderComboBox.Items.Add("Quantity Needed Desc");
            CombinedSortOrderComboBox.SelectedIndex = 0;
        }

        private bool CombinedRequiredItemsFilter(object obj)
        {
            var combinedEntry = obj as CombinedRequiredItemEntry;
            if (combinedEntry == null) return false;

            // Search text filter
            string searchText = CombinedSearchTextBox.Text?.ToLower() ?? string.Empty;
            if (!string.IsNullOrEmpty(searchText))
            {
                if (!combinedEntry.Item.Name.ToLower().Contains(searchText))
                    return false;
            }

            // Recalculate visible quantities
            int totalNeeded = 0;
            int totalOwned = 0;
            bool anyVisible = false;

            foreach (var sourceEntry in combinedEntry.SourceEntries)
            {
                if (IsSourceEntryVisible(sourceEntry))
                {
                    anyVisible = true;
                    totalNeeded += sourceEntry.QuantityNeeded;
                    totalOwned += sourceEntry.QuantityOwned;
                }
            }

            // If no sources are visible after applying hide logic, hide this combined item
            if (!anyVisible)
            {
                return false;
            }

            // Update the combined entry with recalculated visible quantities
            combinedEntry.QuantityNeeded = totalNeeded;
            combinedEntry.QuantityOwned = totalOwned;

            // Completion status filter (based on recalculated totals)
            string selectedCompletion = CombinedCompletionFilterComboBox.SelectedItem as string ?? "All";
            bool isComplete = combinedEntry.QuantityOwned >= combinedEntry.QuantityNeeded;

            if (selectedCompletion == "Remaining Items" && isComplete)
                return false;
            if (selectedCompletion == "Completed Items" && !isComplete)
                return false;

            return true;
        }

        private void CombinedSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CombinedRequiredItemsView?.Refresh();
        }

        private void CombinedCompletionFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CombinedRequiredItemsView?.Refresh();
        }

        private void ApplyCombinedRequiredItemsSorting()
        {
            CombinedRequiredItemsView.SortDescriptions.Clear();

            string selectedSortOrder = CombinedSortOrderComboBox.SelectedItem as string ?? "Default Order";

            if (selectedSortOrder == "Name A-Z")
            {
                CombinedRequiredItemsView.SortDescriptions.Add(new SortDescription("Item.Name", ListSortDirection.Ascending));
            }
            else if (selectedSortOrder == "Name Z-A")
            {
                CombinedRequiredItemsView.SortDescriptions.Add(new SortDescription("Item.Name", ListSortDirection.Descending));
            }
            else if (selectedSortOrder == "Quantity Needed Asc")
            {
                CombinedRequiredItemsView.SortDescriptions.Add(new SortDescription("QuantityNeeded", ListSortDirection.Ascending));
                CombinedRequiredItemsView.SortDescriptions.Add(new SortDescription("Item.Name", ListSortDirection.Ascending));
            }
            else if (selectedSortOrder == "Quantity Needed Desc")
            {
                CombinedRequiredItemsView.SortDescriptions.Add(new SortDescription("QuantityNeeded", ListSortDirection.Descending));
                CombinedRequiredItemsView.SortDescriptions.Add(new SortDescription("Item.Name", ListSortDirection.Ascending));
            }
            else
            {
                // Default order
                // No sorting applied
            }
        }

        private void CombinedSortOrderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyCombinedRequiredItemsSorting();
            CombinedRequiredItemsView?.Refresh();
        }

        public ObservableCollection<CombinedRequiredItemEntry> ManualCombinedRequiredItems { get; set; } = new ObservableCollection<CombinedRequiredItemEntry>();
        public ICollectionView ManualCombinedRequiredItemsView;

        private void LoadManualCombinedRequiredItems()
        {
            var groupedItems = RequiredItems
                .GroupBy(e => new { e.Item.Id, e.Item.Name, e.Item.IconLink, e.IsFoundInRaid })
                .Select(g => new CombinedRequiredItemEntry
                {
                    Item = new Item
                    {
                        Id = g.Key.Id,
                        Name = g.Key.Name,
                        IconLink = g.Key.IconLink
                    },
                    QuantityNeeded = g.Sum(e => e.QuantityNeeded),
                    QuantityOwned = 0, // Start with zero; user adjusts manually
                    SourceEntries = g.ToList(), // Store the original entries
                    IsFoundInRaid = g.Key.IsFoundInRaid,
                    RequiredForDetails = g.Select(e => new SourceDetail
                    {
                        Icon = e.SourceIcon,
                        Name = e.SourceDetail
                    }).Distinct().ToList()
                })
                .ToList();

            ManualCombinedRequiredItems = new ObservableCollection<CombinedRequiredItemEntry>(groupedItems);

            // Setup CollectionView
            ManualCombinedRequiredItemsView = CollectionViewSource.GetDefaultView(ManualCombinedRequiredItems);
            ManualCombinedRequiredItemsView.Filter = ManualCombinedRequiredItemsFilter;
            ManualCombinedRequiredItemsListView.ItemsSource = ManualCombinedRequiredItemsView;

            // Populate filters
            PopulateManualCombinedFilters();

            // Load quantities from file
            LoadManualCombinedQuantities();

            // Apply initial sorting
            ApplyManualCombinedRequiredItemsSorting();
        }

        private bool IsManualSourceEntryVisible(RequiredItemEntry entry)
        {
            // If we need to subtract items from built stations
            if (ConfigWindow.AppConfig.SubtractFromManualCombinedItemsForBuiltStations && entry.GroupType == "Hideout")
            {
                string moduleName = entry.SourceDetail.Split(" Level")[0];
                int level = int.Parse(entry.SourceDetail.Split("Level")[1].Trim());

                var matchingModule = ConfigWindow.AppConfig.EffectiveHideoutModuleSettings
                    .FirstOrDefault(module => module.ModuleName == moduleName);

                if (matchingModule != null && matchingModule.SelectedLevel >= level)
                {
                    // This source is considered "built" and thus should not contribute
                    return false;
                }
            }

            // If we need to subtract plant items
            if (ConfigWindow.AppConfig.SubtractPlantItems)
            {
                if (entry.Item.Name == "WI-FI Camera" || entry.Item.Name == "MS2000 Marker")
                {
                    // This is a plant item, do not count it
                    return false;
                }
            }

            return true;
        }

        private void PopulateManualCombinedFilters()
        {
            // Completion Filter
            ManualCombinedCompletionFilterComboBox.Items.Clear();
            ManualCombinedCompletionFilterComboBox.Items.Add("All");
            ManualCombinedCompletionFilterComboBox.Items.Add("Remaining Items");
            ManualCombinedCompletionFilterComboBox.Items.Add("Completed Items");
            ManualCombinedCompletionFilterComboBox.SelectedIndex = 0;

            // Sort Order
            ManualCombinedSortOrderComboBox.Items.Clear();
            ManualCombinedSortOrderComboBox.Items.Add("Default Order");
            ManualCombinedSortOrderComboBox.Items.Add("Name A-Z");
            ManualCombinedSortOrderComboBox.Items.Add("Name Z-A");
            ManualCombinedSortOrderComboBox.Items.Add("Quantity Needed Asc");
            ManualCombinedSortOrderComboBox.Items.Add("Quantity Needed Desc");
            ManualCombinedSortOrderComboBox.SelectedIndex = 0;
        }

        private bool ManualCombinedRequiredItemsFilter(object obj)
        {
            var combinedEntry = obj as CombinedRequiredItemEntry;
            if (combinedEntry == null) return false;

            // Filter by search text
            string searchText = ManualCombinedSearchTextBox.Text?.ToLower() ?? string.Empty;
            if (!string.IsNullOrEmpty(searchText))
            {
                if (!combinedEntry.Item.Name.ToLower().Contains(searchText))
                    return false;
            }

            // Recalculate based on visible entries
            int totalNeeded = 0;
            int totalOwned = 0;
            bool anyVisible = false;

            foreach (var sourceEntry in combinedEntry.SourceEntries)
            {
                if (IsManualSourceEntryVisible(sourceEntry))
                {
                    anyVisible = true;
                    totalNeeded += sourceEntry.QuantityNeeded;
                    totalOwned += sourceEntry.QuantityOwned;
                }
            }

            // If no sources remain visible, hide the entire combined item
            if (!anyVisible)
            {
                return false;
            }

            // Update the combined entry to show only the remaining visible requirements
            combinedEntry.QuantityNeeded = totalNeeded;
            combinedEntry.QuantityOwned = totalOwned;

            // Filter by completion status
            string selectedCompletion = ManualCombinedCompletionFilterComboBox.SelectedItem as string ?? "All";
            bool isComplete = combinedEntry.QuantityOwned >= combinedEntry.QuantityNeeded;

            if (selectedCompletion == "Remaining Items" && isComplete)
                return false;
            if (selectedCompletion == "Completed Items" && !isComplete)
                return false;

            return true;
        }


        private void ManualCombinedSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ManualCombinedRequiredItemsView?.Refresh();
        }

        private void ManualCombinedCompletionFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ManualCombinedRequiredItemsView?.Refresh();
        }

        private void ManualCombinedIncrementButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var entry = button.DataContext as CombinedRequiredItemEntry;
            if (entry != null)
            {
                if (entry.QuantityOwned < entry.QuantityNeeded)
                {
                    entry.QuantityOwned++;
                    entry.OnPropertyChanged(nameof(entry.QuantityOwned));
                    entry.OnPropertyChanged(nameof(entry.IsComplete));

                    // Save manual combined quantities after change
                    SaveManualCombinedQuantities();
                }
            }
        }

        private void ManualCombinedDecrementButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var entry = button.DataContext as CombinedRequiredItemEntry;
            if (entry != null)
            {
                if (entry.QuantityOwned > 0)
                {
                    entry.QuantityOwned--;
                    entry.OnPropertyChanged(nameof(entry.QuantityOwned));
                    entry.OnPropertyChanged(nameof(entry.IsComplete));

                    // Save manual combined quantities after change
                    SaveManualCombinedQuantities();
                }
            }
        }

        // Save and Load quantities for Manual Combined Required Items
        public void SaveManualCombinedQuantities()
        {
            try
            {
                var quantitiesData = ManualCombinedRequiredItems.Select(e => new RequiredItemQuantity
                {
                    Id = e.Item.Id,
                    QuantityOwned = e.QuantityOwned
                }).ToList();

                string json = JsonConvert.SerializeObject(quantitiesData, Formatting.Indented);
                string manualQuantitiesFile = loadedManualAsPVE ? "manual_combined_quantities_pve.json" : "manual_combined_quantities.json";
                File.WriteAllText(manualQuantitiesFile, json);
                logger.Info($"Manual combined quantities saved to '{manualQuantitiesFile}'.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving manual combined quantities: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                logger.Error(ex, "Failed to save manual combined quantities.");
            }
        }

        private void LoadManualCombinedQuantities()
        {
            try
            {
                string manualQuantitiesFile = App.IsPVEMode ? "manual_combined_quantities_pve.json" : "manual_combined_quantities.json";
                if (App.IsPVEMode)
                {
                    loadedManualAsPVE = true;
                }
                else
                {
                    loadedManualAsPVE = false;
                }

                if (File.Exists(manualQuantitiesFile))
                {
                    string json = File.ReadAllText(manualQuantitiesFile);
                    var quantitiesData = JsonConvert.DeserializeObject<List<RequiredItemQuantity>>(json);
                    foreach (var data in quantitiesData)
                    {
                        var entry = ManualCombinedRequiredItems.FirstOrDefault(e => e.Item.Id == data.Id);
                        if (entry != null)
                        {
                            entry.QuantityOwned = data.QuantityOwned;
                        }
                    }
                }
                else
                {
                    logger.Warn($"Manual combined quantities file '{manualQuantitiesFile}' does not exist.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading manual combined quantities: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                logger.Error(ex, "Failed to load manual combined quantities.");
            }
        }

        private void ApplyManualCombinedRequiredItemsSorting()
        {
            ManualCombinedRequiredItemsView.SortDescriptions.Clear();

            string selectedSortOrder = ManualCombinedSortOrderComboBox.SelectedItem as string ?? "Default Order";

            if (selectedSortOrder == "Name A-Z")
            {
                ManualCombinedRequiredItemsView.SortDescriptions.Add(new SortDescription("Item.Name", ListSortDirection.Ascending));
            }
            else if (selectedSortOrder == "Name Z-A")
            {
                ManualCombinedRequiredItemsView.SortDescriptions.Add(new SortDescription("Item.Name", ListSortDirection.Descending));
            }
            else if (selectedSortOrder == "Quantity Needed Asc")
            {
                ManualCombinedRequiredItemsView.SortDescriptions.Add(new SortDescription("QuantityNeeded", ListSortDirection.Ascending));
                ManualCombinedRequiredItemsView.SortDescriptions.Add(new SortDescription("Item.Name", ListSortDirection.Ascending));
            }
            else if (selectedSortOrder == "Quantity Needed Desc")
            {
                ManualCombinedRequiredItemsView.SortDescriptions.Add(new SortDescription("QuantityNeeded", ListSortDirection.Descending));
                ManualCombinedRequiredItemsView.SortDescriptions.Add(new SortDescription("Item.Name", ListSortDirection.Ascending));
            }
            else
            {
                // Default order
                // No sorting applied
            }
        }

        private void ManualCombinedSortOrderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyManualCombinedRequiredItemsSorting();
            ManualCombinedRequiredItemsView?.Refresh();
        }

        private void SelectStartingTab(string startingTab)
        {
            if (MainTabControl == null)
                return;

            switch (startingTab)
            {
                case "Required Items":
                    MainTabControl.SelectedItem = RequiredItemsTab;
                    break;
                case "Combined Required Items":
                    MainTabControl.SelectedItem = CombinedRequiredItemsTab;
                    break;
                case "Manual Combined Required Items":
                    MainTabControl.SelectedItem = ManualCombinedRequiredItemsTab;
                    break;
                default:
                    MainTabControl.SelectedItem = RequiredItemsTab; // Default to "Manual Combined Required Items"
                    break;
            }
        }

        public async void ReloadData()
        {
            try
            {
                SaveQuantities();
                SaveManualCombinedQuantities();

                // Show loading indicator
                IsRequiredDataLoading = true;

                // Clear existing data
                RequiredItems.Clear();
                CombinedRequiredItems.Clear();
                ManualCombinedRequiredItems.Clear();
                combinedItemDictionary.Clear();

                // Reload data from DataCache
                await DataCache.LoadRequiredItemsData();

                // Re-load required items
                LoadRequiredItems();

                // Re-setup CollectionView for Required Items
                SetupRequiredItemsView();

                // Re-load combined required items
                LoadCombinedRequiredItems();

                // Re-load manual combined required items
                LoadManualCombinedRequiredItems();

                // Re-populate filters
                PopulateFilters();

                // Reload quantities based on current profile mode
                LoadQuantities();
                LoadManualCombinedQuantities();

                // Apply sorting
                ApplyRequiredItemsSorting();
                ApplyCombinedRequiredItemsSorting();
                ApplyManualCombinedRequiredItemsSorting();

                RequiredItemsView?.Refresh();
                CombinedRequiredItemsView?.Refresh();
                ManualCombinedRequiredItemsView.Refresh();

                // Hide loading indicator
                IsRequiredDataLoading = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reloading data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                logger.Error(ex, "Failed to reload data in RequiredItemsWindow.");
            }
        }

        private void SetupRequiredItemsView()
        {
            RequiredItemsView = CollectionViewSource.GetDefaultView(RequiredItems);
            RequiredItemsView.GroupDescriptions.Clear();
            RequiredItemsView.GroupDescriptions.Add(new PropertyGroupDescription("GroupType"));
            RequiredItemsView.Filter = RequiredItemsFilter;
            RequiredItemsListView.ItemsSource = RequiredItemsView;
        }

        private async void AppConfig_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppConfig.HideItemsForBuiltStations) || e.PropertyName == nameof(AppConfig.HidePlantItems))
            {
                if (this.IsVisible)
                {
                    RequiredItemsView.Refresh();
                    CombinedRequiredItemsView.Refresh();
                    ManualCombinedRequiredItemsView.Refresh();
                }
                else
                {
                    MainWindow.refreshRequiredItemsWindow = true;
                }
            }
        }

        // Implement INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));


        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            ConfigWindow.AppConfig.PropertyChanged -= AppConfig_PropertyChanged;

            if (RequiredItems != null)
            {
                foreach (var item in RequiredItems)
                {
                    item.PropertyChanged -= RequiredItemEntry_PropertyChanged;
                }
            }
        }
    }
}
