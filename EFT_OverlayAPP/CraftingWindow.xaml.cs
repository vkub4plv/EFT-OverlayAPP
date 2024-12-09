using EFT_OverlayAPP;
using NLog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace EFT_OverlayAPP
{
    public partial class CraftingWindow : Window, INotifyPropertyChanged
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // Reference to MainWindow to update overlay
        public MainWindow MainWindow { get; set; }
        public ConfigWindow ConfigWindow { get; set; }
        public ObservableCollection<CraftableItem> CraftableItems { get; set; }
        public ObservableCollection<CraftableItem> FavoriteItems { get; set; }
        public ObservableCollection<CraftableItem> ActiveCrafts { get; set; } = new ObservableCollection<CraftableItem>();
        public ICollectionView ActiveCraftsView { get; set; }
        public ICollectionView ItemsView { get; set; }
        public ICollectionView FavoritesView { get; set; }
        public ICollectionView LogsView { get; set; }

        // Dictionary to track active crafts per station
        private Dictionary<string, CraftableItem> activeCraftsPerStation = new Dictionary<string, CraftableItem>();
        private Dictionary<CraftableItem, DispatcherTimer> craftTimers = new Dictionary<CraftableItem, DispatcherTimer>();

        public ObservableCollection<CraftInstance> CraftInstances { get; set; } = new ObservableCollection<CraftInstance>();
        private int craftInstanceIndex = 0; // Index to assign to new craft instances

        public ObservableCollection<CraftStats> CraftStatsCollection { get; set; } = new ObservableCollection<CraftStats>();
        public ICollectionView StatsView { get; set; }

        private bool isLoading;
        public bool IsLoading
        {
            get => isLoading;
            set
            {
                isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        private bool isInitialized = false;

        // Constructor updated to accept MainWindow reference
        public CraftingWindow(MainWindow mainWindow, ConfigWindow configWindow)
        {
            InitializeComponent();
            ConfigWindow = configWindow;
            MainWindow = mainWindow;
            DataContext = this;

            // Initialize collections
            CraftableItems = new ObservableCollection<CraftableItem>();
            FavoriteItems = new ObservableCollection<CraftableItem>();

            // Subscribe to CollectionChanged event
            FavoriteItems.CollectionChanged += FavoriteItems_CollectionChanged;
            ActiveCrafts.CollectionChanged += ActiveCrafts_CollectionChanged;

            // Subscribe to the DataLoaded event
            DataCache.DataLoaded += OnDataLoaded;

            InitializeData();
            isInitialized = true;

            // Subscribe to event handlers after initialization
            SortingComboBox.SelectionChanged += SortingComboBox_SelectionChanged;

            logger.Info("Initializing active crafts from loaded data.");
            // Subscribe to property changes to update the UI
            foreach (var item in DataCache.CraftableItems)
            {
                item.PropertyChanged += CraftableItem_PropertyChanged;

                // If the craft is in progress or ready, update the display
                if (item.CraftStatus != CraftStatus.NotStarted)
                {
                    logger.Info($"Adding active craft: Item ID {item.Id}, Station {item.Station}, Status {item.CraftStatus}");
                    activeCraftsPerStation[item.Station] = item;
                    ActiveCrafts.Add(item);
                    mainWindow.UpdateCraftDisplay(item, remove: false);

                    // Start the timer for this craft
                    StartCraftTimer(item);
                    ActiveCraftsView?.Refresh(); // Refresh the view
                }
            }

            // Set the starting tab
            SelectStartingTab(ConfigWindow.AppConfig.CraftingStartingTab);
        }

        private void OnDataLoaded()
        {
            Dispatcher.Invoke(() =>
            {
                InitializeData();
                IsLoading = false;

                isInitialized = true; // Set initialization flag
            });
        }

        private void InitializeData()
        {
            // Populate the observable collections with cached data
            foreach (var item in DataCache.CraftableItems)
            {
                item.PropertyChanged += Item_PropertyChanged;
                CraftableItems.Add(item);

                if (item.IsFavorite)
                {
                    FavoriteItems.Add(item);
                }
            }

            // Load saved favorite item order
            DataCache.LoadFavoriteItemOrder(FavoriteItems);

            // Set up views and filters
            ComputeCraftStats();
            SetupItemsView();
            SetupFavoritesView();
            SetupActiveCraftsView();
            SetupLogsView();
            SetupStatsView();
            PopulateCategoryFilter();
            PopulateFavoritesCategoryFilter();
            PopulateActiveCraftsCategoryFilter();
            PopulateLogsCategoryFilter();
            PopulateStatsCategoryFilter();

            // Apply initial sorting based on the default selection
            ApplySorting();

            // Load saved craft instances
            var loadedCraftInstances = CraftingDataManager.LoadCraftInstancesData();
            foreach (var craftInstance in loadedCraftInstances)
            {
                // Find the corresponding CraftableItem
                var item = DataCache.CraftableItems.FirstOrDefault(c =>
                    c.Id == craftInstance.CraftableItemId &&
                    c.Station == craftInstance.Station);

                if (item != null)
                {
                    craftInstance.CraftableItem = item;
                    CraftInstances.Add(craftInstance);
                }
                else
                {
                    logger.Warn($"CraftableItem not found for CraftInstance ID: {craftInstance.Id}");
                }
            }

            // Set the craftInstanceIndex to continue from the last index
            if (CraftInstances.Any())
            {
                craftInstanceIndex = CraftInstances.Max(ci => ci.Index) + 1;
            }

            // Recompute stats after loading
            ComputeCraftStats();

            // Refresh views
            ItemsView?.Refresh();
            FavoritesView?.Refresh();
            LogsView?.Refresh();
            StatsView?.Refresh();

            // Subscribe to event handlers
            // (Handled in SubscribeEvents method)

            // Compute and set up Logs and Stats views
            SetupLogsView();
            PopulateLogsCategoryFilter();

            ComputeCraftStats();
            SetupStatsView();
            PopulateStatsCategoryFilter();
        }

        private void SetupItemsView()
        {
            ItemsView = CollectionViewSource.GetDefaultView(CraftableItems);

            // Clear existing group descriptions
            ItemsView.GroupDescriptions.Clear();

            // Group by Station
            ItemsView.GroupDescriptions.Add(new PropertyGroupDescription("Station"));

            ItemsView.Filter = ItemsFilter;

            ItemListView.ItemsSource = ItemsView;
        }

        private void SetupFavoritesView()
        {
            FavoritesView = CollectionViewSource.GetDefaultView(FavoriteItems);

            // Clear existing group descriptions
            FavoritesView.GroupDescriptions.Clear();

            // Group by Station
            FavoritesView.GroupDescriptions.Add(new PropertyGroupDescription("Station"));

            FavoritesView.Filter = FavoritesFilter;

            FavoritesListView.ItemsSource = FavoritesView;
        }

        private void PopulateCategoryFilter()
        {
            // Clear existing items
            CategoryFilterComboBox.Items.Clear();

            // Add "All Categories" as the first item
            CategoryFilterComboBox.Items.Add("All Categories");
            CategoryFilterComboBox.SelectedIndex = 0;

            // Get unique categories from the items
            var categories = new HashSet<string>(CraftableItems.Select(i => i.Station));

            // Add categories to the ComboBox in the static order
            foreach (var category in DataCache.StaticCategoryOrder)
            {
                if (!string.IsNullOrEmpty(category) && categories.Contains(category))
                {
                    CategoryFilterComboBox.Items.Add(category);
                }
            }
        }

        private void PopulateFavoritesCategoryFilter()
        {
            // Clear existing items
            FavoritesCategoryFilterComboBox.Items.Clear();

            // Add "All Categories" as the first item
            FavoritesCategoryFilterComboBox.Items.Add("All Categories");
            FavoritesCategoryFilterComboBox.SelectedIndex = 0;

            // Get unique categories from the favorite items
            var categories = new HashSet<string>(FavoriteItems.Select(i => i.Station));

            // Add categories to the ComboBox in the static order
            foreach (var category in DataCache.StaticCategoryOrder)
            {
                if (!string.IsNullOrEmpty(category) && categories.Contains(category))
                {
                    FavoritesCategoryFilterComboBox.Items.Add(category);
                }
            }
        }

        private void PopulateActiveCraftsCategoryFilter()
        {
            // Clear existing items
            ActiveCraftsCategoryFilterComboBox.Items.Clear();

            // Add "All Categories" as the first item
            ActiveCraftsCategoryFilterComboBox.Items.Add("All Categories");
            ActiveCraftsCategoryFilterComboBox.SelectedIndex = 0;

            // Get unique categories from the active crafts
            var categories = new HashSet<string>(ActiveCrafts.Select(i => i.Station));

            // Add categories to the ComboBox in the static order
            foreach (var category in DataCache.StaticCategoryOrder)
            {
                if (!string.IsNullOrEmpty(category) && categories.Contains(category))
                {
                    ActiveCraftsCategoryFilterComboBox.Items.Add(category);
                }
            }
        }

        private bool ItemsFilter(object item)
        {
            var craftableItem = item as CraftableItem;
            if (craftableItem == null) return false;

            string searchText = SearchTextBox.Text?.ToLower() ?? string.Empty;
            string selectedCategory = CategoryFilterComboBox.SelectedItem as string ?? "All Categories";

            bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                                 craftableItem.RewardItems.Any(r => r.Name.ToLower().Contains(searchText));

            bool matchesCategory = selectedCategory == "All Categories" ||
                                   craftableItem.Station == selectedCategory;

            return matchesSearch && matchesCategory;
        }

        private bool FavoritesFilter(object item)
        {
            var craftableItem = item as CraftableItem;
            if (craftableItem == null) return false;

            string searchText = FavoritesSearchTextBox.Text?.ToLower() ?? string.Empty;
            string selectedCategory = FavoritesCategoryFilterComboBox.SelectedItem as string ?? "All Categories";

            bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                                 craftableItem.RewardItems.Any(r => r.Name.ToLower().Contains(searchText));

            bool matchesCategory = selectedCategory == "All Categories" ||
                                   craftableItem.Station == selectedCategory;

            return craftableItem.IsFavorite && matchesSearch && matchesCategory;
        }

        // Handle Start/Stop/Finish button clicks
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is CraftableItem item)
            {
                HandleCraftAction(item);
            }
        }

        private void HandleCraftAction(CraftableItem item)
        {
            switch (item.CraftStatus)
            {
                case CraftStatus.NotStarted:
                    StartCraft(item);
                    break;
                case CraftStatus.InProgress:
                    StopCraft(item);
                    break;
                case CraftStatus.Ready:
                    FinishCraft(item);
                    break;
            }
        }

        private void StartCraft(CraftableItem item)
        {
            logger.Info($"Attempting to start craft: Item ID {item.Id}, Station {item.Station}");
            // Check for existing craft in the same station
            if (activeCraftsPerStation.TryGetValue(item.Station, out var existingItem))
            {
                if (existingItem.CraftStatus == CraftStatus.InProgress)
                {
                    var result = MessageBox.Show($"Another craft is in progress on {item.Station}. Do you want to replace it?", "Confirm Replace", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        StopCraft(existingItem);
                    }
                    else
                    {
                        return;
                    }
                }
                else if (existingItem.CraftStatus == CraftStatus.Ready)
                {
                    FinishCraft(existingItem);
                }
            }

            // Start the new craft
            item.CraftStatus = CraftStatus.InProgress;
            item.CraftStartTime = DateTime.Now;
            item.CraftCompletedTime = null;
            item.CraftStoppedTime = null;
            item.CraftFinishedTime = null;

            // Notify property changes
            item.OnPropertyChanged(nameof(item.CraftStartTime));
            item.OnPropertyChanged(nameof(item.CraftCompletedTime));
            item.OnPropertyChanged(nameof(item.CraftStoppedTime));
            item.OnPropertyChanged(nameof(item.CraftFinishedTime));

            activeCraftsPerStation[item.Station] = item;

            if (!ActiveCrafts.Contains(item))
            {
                ActiveCrafts.Add(item);
                ActiveCraftsView?.Refresh(); // Refresh the view
            }

            StartCraftTimer(item);
            MainWindow?.UpdateCraftDisplay(item, remove: false);

            // Create and add a new CraftInstance
            var craftInstance = new CraftInstance
            {
                Id = Guid.NewGuid().ToString(),
                CraftableItem = item,
                CraftableItemId = item.Id,
                Station = item.Station,
                Status = CraftInstanceStatus.Started,
                StartTime = DateTime.Now,
                Index = craftInstanceIndex++
            };
            CraftInstances.Add(craftInstance);

            // Save crafts data
            SaveCraftsState();
        }

        private void StopCraft(CraftableItem item)
        {
            logger.Info($"Stopping craft: Item ID {item.Id}, Station {item.Station}");
            // Stop the timer
            if (craftTimers.TryGetValue(item, out var timer))
            {
                timer.Stop();
                craftTimers.Remove(item);
            }

            // Stop and remove the craft
            item.CraftStatus = CraftStatus.NotStarted;
            item.CraftStoppedTime = DateTime.Now;

            // Notify property changes
            item.OnPropertyChanged(nameof(item.CraftStatus));
            item.OnPropertyChanged(nameof(item.CraftStoppedTime));

            activeCraftsPerStation.Remove(item.Station);
            ActiveCrafts.Remove(item);
            MainWindow?.UpdateCraftDisplay(item, remove: true);

            // Update the corresponding CraftInstance
            var craftInstance = FindActiveCraftInstance(item);
            if (craftInstance != null)
            {
                craftInstance.Status = CraftInstanceStatus.Stopped;
                craftInstance.StoppedTime = DateTime.Now;
            }

            // Save crafts data
            SaveCraftsState();
        }

        private void FinishCraft(CraftableItem item)
        {
            logger.Info($"Finishing craft: Item ID {item.Id}, Station {item.Station}");
            // Stop the timer
            if (craftTimers.TryGetValue(item, out var timer))
            {
                timer.Stop();
                craftTimers.Remove(item);
            }

            // Finish and remove the craft
            item.CraftStatus = CraftStatus.NotStarted;
            item.CraftFinishedTime = DateTime.Now;

            // Notify property changes
            item.OnPropertyChanged(nameof(item.CraftStatus));
            item.OnPropertyChanged(nameof(item.CraftFinishedTime));

            activeCraftsPerStation.Remove(item.Station);
            ActiveCrafts.Remove(item);
            MainWindow?.UpdateCraftDisplay(item, remove: true);

            // Existing code to stop the timer and update item status...

            // Update the corresponding CraftInstance
            var craftInstance = FindActiveCraftInstance(item);
            if (craftInstance != null)
            {
                craftInstance.Status = CraftInstanceStatus.Finished;
                craftInstance.FinishedTime = DateTime.Now;
                logger.Info($"CraftInstance Updated: ID={craftInstance.Id}, Status={craftInstance.Status}, FinishedTime={craftInstance.FinishedTime}");
            }
            else
            {
                logger.Warn($"No active CraftInstance found for item ID {item.Id} at station {item.Station} to set FinishedTime.");
            }

            // Save crafts data
            SaveCraftsState();
        }

        private void SaveCraftsState()
        {
            LogsView?.Refresh();
            StatsView?.Refresh();

            // Get all crafts that have been started (including those that are Ready)
            var activeCrafts = DataCache.CraftableItems
                .Where(c => c.CraftStatus != CraftStatus.NotStarted)
                .ToList();

            CraftingDataManager.SaveCraftsData(activeCrafts);

            // Save craft instances
            CraftingDataManager.SaveCraftInstancesData(CraftInstances.ToList());
        }

        private CraftInstance FindActiveCraftInstance(CraftableItem item)
        {
            // Find the most recent CraftInstance for the given item that is either Started or Completed
            return CraftInstances
                .Where(ci => ci.CraftableItem.Id == item.Id && ci.CraftableItem.Station == item.Station)
                .OrderByDescending(ci => ci.Index) // Ensure we get the latest instance
                .FirstOrDefault(ci => ci.Status == CraftInstanceStatus.Started || ci.Status == CraftInstanceStatus.Completed);
        }


        private void StartCraftTimer(CraftableItem item)
        {
            logger.Info($"Starting timer for craft: Item ID {item.Id}, Station {item.Station}");
            // If a timer already exists for this item, don't create a new one
            if (craftTimers.ContainsKey(item))
                return;

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);

            timer.Tick += (sender, e) =>
            {
                item.OnPropertyChanged(nameof(CraftableItem.RemainingTime));
                item.OnPropertyChanged(nameof(CraftableItem.RemainingTimeString));

                if (item.RemainingTime <= TimeSpan.Zero)
                {
                    timer.Stop();
                    craftTimers.Remove(item);

                    item.CraftStatus = CraftStatus.Ready;
                    item.CraftCompletedTime = item.CraftStartTime.Value.Add(item.CraftTime); 
                    item.OnPropertyChanged(nameof(CraftableItem.CraftCompletedTime));

                    // Update the CraftInstance
                    var craftInstance = FindActiveCraftInstance(item);
                    if (craftInstance != null)
                    {
                        craftInstance.Status = CraftInstanceStatus.Completed;
                        craftInstance.CompletedTime = DateTime.Now;
                    }

                    // Notify the main window to update the display
                    MainWindow?.UpdateCraftDisplay(item, remove: false);

                    SaveCraftsState();
                }
            };

            timer.Start();
            craftTimers[item] = timer;
        }

        private void SetupActiveCraftsView()
        {
            ActiveCraftsView = CollectionViewSource.GetDefaultView(ActiveCrafts);

            // Clear existing group descriptions
            ActiveCraftsView.GroupDescriptions.Clear();

            // Group by Station
            ActiveCraftsView.GroupDescriptions.Add(new PropertyGroupDescription("Station"));

            ActiveCraftsView.Filter = ActiveCraftsFilter;

            ActiveCraftsListView.ItemsSource = ActiveCraftsView;
        }


        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ItemsView.Refresh();
        }

        private void CategoryFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ItemsView.Refresh();
        }

        private void FavoritesSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FavoritesView.Refresh();
        }

        private void FavoritesCategoryFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FavoritesView.Refresh();
        }

        private void SortingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isInitialized)
            {
                return;
            }

            ApplySorting();
        }

        private void ActiveCrafts_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            PopulateActiveCraftsCategoryFilter();
            ActiveCraftsView?.Refresh();
        }

        private void ActiveCraftsSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ActiveCraftsView?.Refresh();
        }

        private void ActiveCraftsCategoryFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ActiveCraftsView?.Refresh();
        }

        private bool ActiveCraftsFilter(object item)
        {
            var craftableItem = item as CraftableItem;
            if (craftableItem == null) return false;

            string searchText = ActiveCraftsSearchTextBox.Text?.ToLower() ?? string.Empty;
            string selectedCategory = ActiveCraftsCategoryFilterComboBox.SelectedItem as string ?? "All Categories";

            bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                                 craftableItem.RewardItems.Any(r => r.Name.ToLower().Contains(searchText));

            bool matchesCategory = selectedCategory == "All Categories" ||
                                   craftableItem.Station == selectedCategory;

            return matchesSearch && matchesCategory;
        }

        private void ApplySorting()
        {
            if (ItemsView == null || FavoritesView == null)
            {
                return;
            }

            var selectedSorting = (SortingComboBox.SelectedItem as ComboBoxItem)?.Content as string;

            // Apply sorting to All Items
            ItemsView.SortDescriptions.Clear();
            ItemsView.SortDescriptions.Add(new SortDescription(nameof(CraftableItem.StationIndex), ListSortDirection.Ascending));

            if (selectedSorting == "Name (A-Z)")
            {
                ItemsView.SortDescriptions.Add(new SortDescription(nameof(CraftableItem.FirstRewardItemName), ListSortDirection.Ascending));
            }
            else if (selectedSorting == "Name (Z-A)")
            {
                ItemsView.SortDescriptions.Add(new SortDescription(nameof(CraftableItem.FirstRewardItemName), ListSortDirection.Descending));
            }
            else
            {
                ItemsView.SortDescriptions.Add(new SortDescription(nameof(CraftableItem.OriginalIndex), ListSortDirection.Ascending));
            }

            ItemsView.Refresh();

            // Apply sorting to Favorites
            FavoritesView.SortDescriptions.Clear();
            FavoritesView.SortDescriptions.Add(new SortDescription(nameof(CraftableItem.StationIndex), ListSortDirection.Ascending));
            FavoritesView.SortDescriptions.Add(new SortDescription(nameof(CraftableItem.FavoriteSortOrder), ListSortDirection.Ascending));

            if (selectedSorting == "Name (A-Z)")
            {
                FavoritesView.SortDescriptions.Add(new SortDescription(nameof(CraftableItem.FirstRewardItemName), ListSortDirection.Ascending));
            }
            else if (selectedSorting == "Name (Z-A)")
            {
                FavoritesView.SortDescriptions.Add(new SortDescription(nameof(CraftableItem.FirstRewardItemName), ListSortDirection.Descending));
            }

            FavoritesView.Refresh();
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CraftableItem.IsFavorite))
            {
                var item = sender as CraftableItem;

                // Avoid duplicate entries in FavoriteItems
                if (item.IsFavorite && !FavoriteItems.Contains(item))
                {
                    item.FavoriteSortOrder = FavoriteItems.Count; // Assign the next sort order
                    FavoriteItems.Add(item);
                    DataCache.AddFavoriteId(item.Id, ConfigWindow);
                }
                else if (!item.IsFavorite && FavoriteItems.Contains(item))
                {
                    FavoriteItems.Remove(item);
                    DataCache.RemoveFavoriteId(item.Id, ConfigWindow);
                }

                // Update the favorites category filter
                PopulateFavoritesCategoryFilter();
                FavoritesView.Refresh();
            }
        }

        private void FavoriteItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Move ||
                e.Action == NotifyCollectionChangedAction.Add ||
                e.Action == NotifyCollectionChangedAction.Remove)
            {
                DataCache.SaveFavoriteItemOrder(FavoriteItems);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            // Cancel the closing event
            e.Cancel = true;

            // Hide the window instead of closing
            this.Hide();

            // Save crafts and craft instances data
            SaveCraftsState();

            // Unsubscribe from events to prevent multiple subscriptions
            DataCache.DataLoaded -= OnDataLoaded;
        }

        // Implement INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Drag-and-Drop Implementation for FavoritesListView

        private Point _startPoint;

        private void FavoritesListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Store the mouse position
            _startPoint = e.GetPosition(null);
        }

        private void FavoritesListView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = _startPoint - mousePos;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                // Get the dragged ListViewItem
                ListView listView = sender as ListView;
                ListViewItem listViewItem = FindAncestor<ListViewItem>((DependencyObject)e.OriginalSource);

                if (listViewItem == null)
                    return;

                // Get the data bound to the ListViewItem
                CraftableItem draggedItem = listView.ItemContainerGenerator.ItemFromContainer(listViewItem) as CraftableItem;

                if (draggedItem != null)
                {
                    // Initialize the drag-and-drop operation
                    DataObject dragData = new DataObject("CraftableItemFormat", draggedItem);
                    DragDrop.DoDragDrop(listViewItem, dragData, DragDropEffects.Move);
                }
            }
        }

        private void FavoritesListView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("CraftableItemFormat"))
            {
                CraftableItem droppedData = e.Data.GetData("CraftableItemFormat") as CraftableItem;
                ListView listView = sender as ListView;

                // Get the drop position
                Point position = e.GetPosition(listView);
                HitTestResult result = VisualTreeHelper.HitTest(listView, position);

                // Try to find the target item
                ListViewItem listViewItem = FindAncestor<ListViewItem>(result.VisualHit);

                int removedIdx = FavoriteItems.IndexOf(droppedData);
                int targetIdx = FavoriteItems.Count; // Default to end

                if (listViewItem != null)
                {
                    CraftableItem targetData = listView.ItemContainerGenerator.ItemFromContainer(listViewItem) as CraftableItem;
                    targetIdx = FavoriteItems.IndexOf(targetData);
                }
                else
                {
                    // Handle dropping on group headers or empty spaces
                    GroupItem groupItem = FindAncestor<GroupItem>(result.VisualHit);
                    if (groupItem != null)
                    {
                        CollectionViewGroup group = groupItem.Content as CollectionViewGroup;
                        if (group != null && group.ItemCount > 0)
                        {
                            var firstItemInGroup = group.Items[0] as CraftableItem;
                            targetIdx = FavoriteItems.IndexOf(firstItemInGroup);
                        }
                    }
                }

                if (removedIdx >= 0 && targetIdx >= 0 && removedIdx != targetIdx)
                {
                    // Prevent moving items across different categories
                    var sourceItem = FavoriteItems[removedIdx];
                    var targetItem = targetIdx < FavoriteItems.Count ? FavoriteItems[targetIdx] : null;

                    if (targetItem != null && !sourceItem.Station.Equals(targetItem.Station, StringComparison.OrdinalIgnoreCase))
                    {
                        // Inform the user that cross-category reordering is not allowed
                        MessageBox.Show("You can only reorder items within the same category.", "Reordering Not Allowed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Move the item
                    FavoriteItems.Move(removedIdx, targetIdx);

                    // Update FavoriteSortOrder based on new positions within each category
                    UpdateFavoriteSortOrder();

                    // Refresh the view to apply new sort orders
                    FavoritesView.Refresh();

                    // Save the updated order
                    DataCache.SaveFavoriteItemOrder(FavoriteItems);
                }
            }
        }


        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T)
                {
                    return (T)current;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }

        private void FavoritesResetOrderButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(ConfigWindow.AppConfig.FavoritesItemOrderFileName))
            {
                File.Delete(ConfigWindow.AppConfig.FavoritesItemOrderFileName);
            }

            IsLoading = true; // Show loading indicator

            // Clear the FavoriteItems collection
            FavoriteItems.Clear();

            // Re-populate FavoriteItems based on current favorites in static category order
            foreach (var category in DataCache.StaticCategoryOrder)
            {
                var itemsInCategory = DataCache.CraftableItems
                    .Where(i => i.IsFavorite && i.Station == category)
                    .OrderBy(i => i.OriginalIndex) // Preserve original API order within category
                    .ToList();

                foreach (var item in itemsInCategory)
                {
                    item.FavoriteSortOrder = FavoriteItems.Count;
                    FavoriteItems.Add(item);
                }
            }

            // Refresh the favorites view
            ApplySorting(); // Re-apply sorting to FavoritesView

            IsLoading = false; // Hide loading indicator
        }

        private void UpdateFavoriteSortOrder()
        {
            // Assign FavoriteSortOrder within each category based on the static category order
            foreach (var category in DataCache.StaticCategoryOrder)
            {
                var itemsInCategory = FavoriteItems
                    .Where(i => i.Station.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(i => FavoriteItems.IndexOf(i)) // Maintain current order within category
                    .ToList();

                for (int i = 0; i < itemsInCategory.Count; i++)
                {
                    itemsInCategory[i].FavoriteSortOrder = i;
                }
            }

            // Assign FavoriteSortOrder to items not in any predefined category
            var unknownCategoryItems = FavoriteItems
                .Where(i => !DataCache.StaticCategoryOrder.Contains(i.Station, StringComparer.OrdinalIgnoreCase))
                .ToList();

            for (int i = 0; i < unknownCategoryItems.Count; i++)
            {
                unknownCategoryItems[i].FavoriteSortOrder = i;
            }
        }

        private void CraftableItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var item = sender as CraftableItem;
            if (item != null)
            {
                if (e.PropertyName == nameof(CraftableItem.CraftStatus))
                {
                    if (item.CraftStatus == CraftStatus.InProgress || item.CraftStatus == CraftStatus.Ready)
                    {
                        MainWindow?.UpdateCraftDisplay(item, remove: false);
                    }
                    else if (item.CraftStatus == CraftStatus.NotStarted)
                    {
                        MainWindow?.UpdateCraftDisplay(item, remove: true);
                    }
                }
            }
        }

        private void SetupLogsView()
        {
            LogsView = CollectionViewSource.GetDefaultView(CraftInstances);
            LogsView.GroupDescriptions.Clear();

            // Group by Station
            var groupDescription = new PropertyGroupDescription("CraftableItem.Station");
            LogsView.GroupDescriptions.Add(groupDescription);

            // Ensure the groups follow the StaticCategoryOrder
            LogsView.SortDescriptions.Clear();
            LogsView.SortDescriptions.Add(new SortDescription("CraftableItem.StationIndex", ListSortDirection.Ascending));
            LogsView.SortDescriptions.Add(new SortDescription("StartTime", ListSortDirection.Descending));

            LogsView.Filter = LogsFilter;

            LogsListView.ItemsSource = LogsView;
        }

        // Method to populate LogsCategoryFilterComboBox
        private void PopulateLogsCategoryFilter()
        {
            // Clear existing items
            LogsCategoryFilterComboBox.Items.Clear();

            // Add "All Categories" as the first item
            LogsCategoryFilterComboBox.Items.Add("All Categories");
            LogsCategoryFilterComboBox.SelectedIndex = 0;

            // Get unique categories from the CraftInstances
            var categories = new HashSet<string>(CraftInstances.Select(ci => ci.CraftableItem.Station));

            // Add categories to the ComboBox in the StaticCategoryOrder
            foreach (var category in DataCache.StaticCategoryOrder)
            {
                if (!string.IsNullOrEmpty(category) && categories.Contains(category))
                {
                    LogsCategoryFilterComboBox.Items.Add(category);
                }
            }
        }

        private bool LogsFilter(object item)
        {
            var craftInstance = item as CraftInstance;
            if (craftInstance == null) return false;

            string searchText = LogsSearchTextBox.Text?.ToLower() ?? string.Empty;
            string selectedCategory = LogsCategoryFilterComboBox.SelectedItem as string ?? "All Categories";

            bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                                 craftInstance.CraftableItem.RewardItems.Any(r => r.Name.ToLower().Contains(searchText));

            bool matchesCategory = selectedCategory == "All Categories" ||
                                   craftInstance.CraftableItem.Station == selectedCategory;

            return matchesSearch && matchesCategory;
        }

        private void LogsSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            LogsView?.Refresh();
        }

        private void LogsCategoryFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LogsView?.Refresh();
        }

        private void LogsSortingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyLogsSorting();
        }

        private void ApplyLogsSorting()
        {
            if (LogsView == null)
                return;

            var selectedSorting = (LogsSortingComboBox.SelectedItem as ComboBoxItem)?.Content as string;

            LogsView.SortDescriptions.Clear();
            LogsView.SortDescriptions.Add(new SortDescription("CraftableItem.StationIndex", ListSortDirection.Ascending));

            if (selectedSorting == "Most Recent")
            {
                LogsView.SortDescriptions.Add(new SortDescription("Index", ListSortDirection.Descending));
            }
            else if (selectedSorting == "Oldest")
            {
                LogsView.SortDescriptions.Add(new SortDescription("Index", ListSortDirection.Ascending));
            }
            else if (selectedSorting == "Name (A-Z)")
            {
                LogsView.SortDescriptions.Add(new SortDescription("CraftableItem.FirstRewardItemName", ListSortDirection.Ascending));
            }
            else if (selectedSorting == "Name (Z-A)")
            {
                LogsView.SortDescriptions.Add(new SortDescription("CraftableItem.FirstRewardItemName", ListSortDirection.Descending));
            }

            LogsView.Refresh();
        }

        private void ComputeCraftStats()
        {
            CraftStatsCollection.Clear();

            var groupedByItem = CraftInstances.GroupBy(ci => ci.CraftableItem.Id);

            foreach (var group in groupedByItem)
            {
                var craftableItem = group.First().CraftableItem;
                var stats = new CraftStats
                {
                    CraftableItem = craftableItem,
                    // Include all crafts that were started, regardless of their final status
                    TimesStarted = group.Count(),
                    // Count crafts that were stopped
                    TimesStopped = group.Count(ci => ci.Status == CraftInstanceStatus.Stopped),
                    // Count crafts that were completed or finished
                    TimesCompleted = group.Count(ci => ci.Status == CraftInstanceStatus.Completed || ci.Status == CraftInstanceStatus.Finished),
                    // Earliest start time
                    FirstStartedTime = group.Min(ci => ci.StartTime),
                    // Latest start time
                    LastStartedTime = group.Max(ci => ci.StartTime),
                    // Latest stopped time, if any
                    LastStoppedTime = group.Where(ci => ci.StoppedTime.HasValue).Any() ? group.Where(ci => ci.StoppedTime.HasValue).Max(ci => ci.StoppedTime) : null,
                    // Latest completed time, if any
                    LastCompletedTime = group.Where(ci => ci.CompletedTime.HasValue).Any() ? group.Where(ci => ci.CompletedTime.HasValue).Max(ci => ci.CompletedTime) : null
                };
                CraftStatsCollection.Add(stats);
            }
        }

        private void SetupStatsView()
        {
            StatsView = CollectionViewSource.GetDefaultView(CraftStatsCollection);
            StatsView.GroupDescriptions.Clear();

            // Group by Station
            var groupDescription = new PropertyGroupDescription("CraftableItem.Station");
            StatsView.GroupDescriptions.Add(groupDescription);

            // Ensure the groups follow the StaticCategoryOrder
            StatsView.SortDescriptions.Clear();
            StatsView.SortDescriptions.Add(new SortDescription("CraftableItem.StationIndex", ListSortDirection.Ascending));
            StatsView.SortDescriptions.Add(new SortDescription("TimesStarted", ListSortDirection.Descending));

            StatsView.Filter = StatsFilter;

            StatsListView.ItemsSource = StatsView;
        }

        // Method to populate StatsCategoryFilterComboBox
        private void PopulateStatsCategoryFilter()
        {
            // Clear existing items
            StatsCategoryFilterComboBox.Items.Clear();

            // Add "All Categories" as the first item
            StatsCategoryFilterComboBox.Items.Add("All Categories");
            StatsCategoryFilterComboBox.SelectedIndex = 0;

            // Get unique categories from the CraftStatsCollection
            var categories = new HashSet<string>(CraftStatsCollection.Select(cs => cs.CraftableItem.Station));

            // Add categories to the ComboBox in the StaticCategoryOrder
            foreach (var category in DataCache.StaticCategoryOrder)
            {
                if (!string.IsNullOrEmpty(category) && categories.Contains(category))
                {
                    StatsCategoryFilterComboBox.Items.Add(category);
                }
            }
        }

        private bool StatsFilter(object item)
        {
            var craftStats = item as CraftStats;
            if (craftStats == null) return false;

            string searchText = StatsSearchTextBox.Text?.ToLower() ?? string.Empty;
            string selectedCategory = StatsCategoryFilterComboBox.SelectedItem as string ?? "All Categories";

            bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                                 craftStats.CraftableItem.RewardItems.Any(r => r.Name.ToLower().Contains(searchText));

            bool matchesCategory = selectedCategory == "All Categories" ||
                                   craftStats.CraftableItem.Station == selectedCategory;

            return matchesSearch && matchesCategory;
        }

        private void StatsSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            StatsView?.Refresh();
        }

        private void StatsCategoryFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StatsView?.Refresh();
        }

        private void StatsSortingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyStatsSorting();
        }

        private void ApplyStatsSorting()
        {
            if (StatsView == null)
                return;

            var selectedSorting = (StatsSortingComboBox.SelectedItem as ComboBoxItem)?.Content as string;

            StatsView.SortDescriptions.Clear();
            StatsView.SortDescriptions.Add(new SortDescription("CraftableItem.StationIndex", ListSortDirection.Ascending));

            if (selectedSorting == "Most Recent")
            {
                StatsView.SortDescriptions.Add(new SortDescription("FirstStartedTime", ListSortDirection.Descending));
            }
            else if (selectedSorting == "Oldest")
            {
                StatsView.SortDescriptions.Add(new SortDescription("FirstStartedTime", ListSortDirection.Ascending));
            }
            else if (selectedSorting == "Name (A-Z)")
            {
                StatsView.SortDescriptions.Add(new SortDescription("CraftableItem.FirstRewardItemName", ListSortDirection.Ascending));
            }
            else if (selectedSorting == "Name (Z-A)")
            {
                StatsView.SortDescriptions.Add(new SortDescription("CraftableItem.FirstRewardItemName", ListSortDirection.Descending));
            }
            else
            {
                // Default sorting by FirstStartedTime
                StatsView.SortDescriptions.Add(new SortDescription("FirstStartedTime", ListSortDirection.Ascending));
            }

            StatsView.Refresh();
        }

        // Event handler when CraftInstances change
        private void CraftInstances_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            ComputeCraftStats();
            LogsView?.Refresh();
            StatsView?.Refresh();

            // Update category filters
            PopulateLogsCategoryFilter();
            PopulateStatsCategoryFilter();
        }

        private void SelectStartingTab(string startingTab)
        {
            if (MainTabControl == null)
                return;

            switch (startingTab)
            {
                case "All Items":
                    MainTabControl.SelectedItem = AllItemsTab;
                    break;
                case "Favorites":
                    MainTabControl.SelectedItem = FavoritesTab;
                    break;
                case "Active Crafts":
                    MainTabControl.SelectedItem = ActiveCraftsTab;
                    break;
                case "Logs":
                    MainTabControl.SelectedItem = LogsTab;
                    break;
                case "Stats":
                    MainTabControl.SelectedItem = StatsTab;
                    break;
                default:
                    MainTabControl.SelectedItem = AllItemsTab; // Default to "All Items"
                    break;
            }
        }

        public async void ReloadData()
        {
            try
            {
                if (IsLoading)
                {
                    logger.Info("ReloadData called, but data is already loading.");
                    return;
                }

                IsLoading = true;
                logger.Info("Reloading data in CraftingWindow.");

                // Unsubscribe from existing events to prevent duplicate handlers
                UnsubscribeEvents();

                // Clear existing collections
                ClearCollections();

                // Reload data from DataCache
                await DataCache.LoadDataAsync(ConfigWindow);

                // Reinitialize data
                InitializeData();

                // Re-subscribe to events
                SubscribeEvents();

                // Reinitialize crafting timers based on MainWindow's active crafts
                InitializeActiveCraftTimers();

                // Refresh all views
                RefreshAllViews();

                // Re-populate category filters
                PopulateAllCategoryFilters();

                // Apply current sorting
                ApplySorting();

                logger.Info("Data reloaded successfully in CraftingWindow.");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error reloading data in CraftingWindow.");
                MessageBox.Show($"Error reloading data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Helper methods

        private void UnsubscribeEvents()
        {
            // Unsubscribe from existing event handlers
            FavoriteItems.CollectionChanged -= FavoriteItems_CollectionChanged;
            ActiveCrafts.CollectionChanged -= ActiveCrafts_CollectionChanged;
            DataCache.DataLoaded -= OnDataLoaded;

            SortingComboBox.SelectionChanged -= SortingComboBox_SelectionChanged;
            SearchTextBox.TextChanged -= SearchTextBox_TextChanged;
            CategoryFilterComboBox.SelectionChanged -= CategoryFilterComboBox_SelectionChanged;
            FavoritesSearchTextBox.TextChanged -= FavoritesSearchTextBox_TextChanged;
            FavoritesCategoryFilterComboBox.SelectionChanged -= FavoritesCategoryFilterComboBox_SelectionChanged;
            ActiveCraftsSearchTextBox.TextChanged -= ActiveCraftsSearchTextBox_TextChanged;
            ActiveCraftsCategoryFilterComboBox.SelectionChanged -= ActiveCraftsCategoryFilterComboBox_SelectionChanged;
            CraftInstances.CollectionChanged -= CraftInstances_CollectionChanged;

            LogsSearchTextBox.TextChanged -= LogsSearchTextBox_TextChanged;
            LogsCategoryFilterComboBox.SelectionChanged -= LogsCategoryFilterComboBox_SelectionChanged;
            LogsSortingComboBox.SelectionChanged -= LogsSortingComboBox_SelectionChanged;

            StatsSearchTextBox.TextChanged -= StatsSearchTextBox_TextChanged;
            StatsCategoryFilterComboBox.SelectionChanged -= StatsCategoryFilterComboBox_SelectionChanged;
            StatsSortingComboBox.SelectionChanged -= StatsSortingComboBox_SelectionChanged;
        }

        private void SubscribeEvents()
        {
            // Subscribe to events again
            FavoriteItems.CollectionChanged += FavoriteItems_CollectionChanged;
            ActiveCrafts.CollectionChanged += ActiveCrafts_CollectionChanged;
            DataCache.DataLoaded += OnDataLoaded;

            SortingComboBox.SelectionChanged += SortingComboBox_SelectionChanged;
            SearchTextBox.TextChanged += SearchTextBox_TextChanged;
            CategoryFilterComboBox.SelectionChanged += CategoryFilterComboBox_SelectionChanged;
            FavoritesSearchTextBox.TextChanged += FavoritesSearchTextBox_TextChanged;
            FavoritesCategoryFilterComboBox.SelectionChanged += FavoritesCategoryFilterComboBox_SelectionChanged;
            ActiveCraftsSearchTextBox.TextChanged += ActiveCraftsSearchTextBox_TextChanged;
            ActiveCraftsCategoryFilterComboBox.SelectionChanged += ActiveCraftsCategoryFilterComboBox_SelectionChanged;
            CraftInstances.CollectionChanged += CraftInstances_CollectionChanged;

            LogsSearchTextBox.TextChanged += LogsSearchTextBox_TextChanged;
            LogsCategoryFilterComboBox.SelectionChanged += LogsCategoryFilterComboBox_SelectionChanged;
            LogsSortingComboBox.SelectionChanged += LogsSortingComboBox_SelectionChanged;

            StatsSearchTextBox.TextChanged += StatsSearchTextBox_TextChanged;
            StatsCategoryFilterComboBox.SelectionChanged += StatsCategoryFilterComboBox_SelectionChanged;
            StatsSortingComboBox.SelectionChanged += StatsSortingComboBox_SelectionChanged;
        }

        private void ClearCollections()
        {
            // Unsubscribe from item property changed events
            foreach (var item in CraftableItems)
            {
                item.PropertyChanged -= Item_PropertyChanged;
            }

            CraftableItems.Clear();
            FavoriteItems.Clear();
            ActiveCrafts.Clear();
            CraftInstances.Clear();
            CraftStatsCollection.Clear();

            // Clear craftTimers and activeCraftsPerStation
            craftTimers.Clear();
            activeCraftsPerStation.Clear();
        }

        private void InitializeActiveCraftTimers()
        {
            // Iterate through CraftableItems to initialize active crafts based on their status
            foreach (var item in CraftableItems)
            {
                if (item.CraftStatus == CraftStatus.InProgress || item.CraftStatus == CraftStatus.Ready)
                {
                    if (!activeCraftsPerStation.ContainsKey(item.Station))
                    {
                        activeCraftsPerStation[item.Station] = item;
                        ActiveCrafts.Add(item);
                        MainWindow?.UpdateCraftDisplay(item, remove: false);

                        // Start the timer for this craft
                        StartCraftTimer(item);
                    }
                }
            }
        }

        private void RefreshAllViews()
        {
            ItemsView?.Refresh();
            FavoritesView?.Refresh();
            ActiveCraftsView?.Refresh();
            LogsView?.Refresh();
            StatsView?.Refresh();
        }

        private void PopulateAllCategoryFilters()
        {
            PopulateCategoryFilter();
            PopulateFavoritesCategoryFilter();
            PopulateActiveCraftsCategoryFilter();
            PopulateLogsCategoryFilter();
            PopulateStatsCategoryFilter();
        }
    }
}
