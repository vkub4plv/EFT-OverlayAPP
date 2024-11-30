using EFT_OverlayAPP;
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
        // Reference to MainWindow to update overlay
        public MainWindow MainWindow { get; set; }
        public ObservableCollection<CraftableItem> CraftableItems { get; set; }
        public ObservableCollection<CraftableItem> FavoriteItems { get; set; }
        public ObservableCollection<CraftableItem> ActiveCrafts { get; set; } = new ObservableCollection<CraftableItem>();
        public ICollectionView ActiveCraftsView { get; set; }
        public ICollectionView ItemsView { get; set; }
        public ICollectionView FavoritesView { get; set; }
        // Dictionary to track active crafts per station
        private Dictionary<string, CraftableItem> activeCraftsPerStation = new Dictionary<string, CraftableItem>();

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
        public CraftingWindow(MainWindow mainWindow)
        {
            InitializeComponent();
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

            // Check if data is already loaded
            if (DataCache.IsDataLoaded)
            {
                InitializeData();
                isInitialized = true;
            }
            else
            {
                IsLoading = true;
                Task.Run(() => DataCache.LoadDataAsync());
            }

            // Subscribe to event handlers after initialization
            SortingComboBox.SelectionChanged += SortingComboBox_SelectionChanged;

            // Subscribe to property changes to update the UI
            foreach (var item in DataCache.CraftableItems)
            {
                item.PropertyChanged += CraftableItem_PropertyChanged;

                // If the craft is in progress or ready, update the display
                if (item.CraftStatus != CraftStatus.NotStarted)
                {
                    mainWindow.UpdateCraftDisplay(item, false);
                }
            }
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
            // Unsubscribe from event handlers to prevent multiple subscriptions
            foreach (var item in CraftableItems)
            {
                item.PropertyChanged -= Item_PropertyChanged;
            }

            // Clear existing items
            CraftableItems.Clear();
            FavoriteItems.Clear();

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
            SetupItemsView();
            SetupFavoritesView();
            SetupActiveCraftsView();
            PopulateCategoryFilter();
            PopulateFavoritesCategoryFilter();
            PopulateActiveCraftsCategoryFilter();

            // Apply initial sorting based on the default selection
            ApplySorting();

            // Refresh views
            ItemsView?.Refresh();
            FavoritesView?.Refresh();

            // Subscribe to event handlers
            SearchTextBox.TextChanged += SearchTextBox_TextChanged;
            CategoryFilterComboBox.SelectionChanged += CategoryFilterComboBox_SelectionChanged;

            FavoritesSearchTextBox.TextChanged += FavoritesSearchTextBox_TextChanged;
            FavoritesCategoryFilterComboBox.SelectionChanged += FavoritesCategoryFilterComboBox_SelectionChanged;

            ActiveCraftsSearchTextBox.TextChanged += ActiveCraftsSearchTextBox_TextChanged;
            ActiveCraftsCategoryFilterComboBox.SelectionChanged += ActiveCraftsCategoryFilterComboBox_SelectionChanged;
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

            // Save crafts data
            SaveCraftsState();
        }

        private void StopCraft(CraftableItem item)
        {
            // Stop and remove the craft
            item.CraftStatus = CraftStatus.NotStarted;
            item.CraftStoppedTime = DateTime.Now;

            // Notify property changes
            item.OnPropertyChanged(nameof(item.CraftStatus));
            item.OnPropertyChanged(nameof(item.CraftStoppedTime));

            activeCraftsPerStation.Remove(item.Station);
            ActiveCrafts.Remove(item);
            MainWindow?.UpdateCraftDisplay(item, remove: true);

            // Save crafts data
            SaveCraftsState();
        }

        private void FinishCraft(CraftableItem item)
        {
            // Finish and remove the craft
            item.CraftStatus = CraftStatus.NotStarted;
            item.CraftFinishedTime = DateTime.Now;

            // If the craft has not yet been marked as completed, set the completed time
            if (!item.CraftCompletedTime.HasValue)
            {
                item.CraftCompletedTime = item.CraftStartTime?.Add(item.CraftTime);
            }

            // Notify property changes
            item.OnPropertyChanged(nameof(item.CraftStatus));
            item.OnPropertyChanged(nameof(item.CraftFinishedTime));
            item.OnPropertyChanged(nameof(item.CraftCompletedTime));

            activeCraftsPerStation.Remove(item.Station);
            ActiveCrafts.Remove(item);
            MainWindow?.UpdateCraftDisplay(item, remove: true);

            // Save crafts data
            SaveCraftsState();
        }

        private void SaveCraftsState()
        {
            // Get all crafts that have been started (including those that are Ready)
            var activeCrafts = DataCache.CraftableItems
                .Where(c => c.CraftStatus != CraftStatus.NotStarted)
                .ToList();

            CraftingDataManager.SaveCraftsData(activeCrafts);
        }

        private void StartCraftTimer(CraftableItem item)
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            timer.Tick += (s, e) =>
            {
                if (item.CraftStatus == CraftStatus.InProgress)
                {
                    if (item.RemainingTime <= TimeSpan.Zero)
                    {
                        item.CraftStatus = CraftStatus.Ready;
                        // Raise PropertyChanged for RemainingTime and RemainingTimeString
                        item.OnPropertyChanged(nameof(CraftableItem.RemainingTime));
                        item.OnPropertyChanged(nameof(CraftableItem.RemainingTimeString));
                        timer.Stop();
                    }
                    else
                    {
                        item.OnPropertyChanged(nameof(CraftableItem.RemainingTime));
                    }
                }
                else
                {
                    timer.Stop();
                }
            };

            timer.Start();
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
                    DataCache.AddFavoriteId(item.Id);
                }
                else if (!item.IsFavorite && FavoriteItems.Contains(item))
                {
                    FavoriteItems.Remove(item);
                    DataCache.RemoveFavoriteId(item.Id);
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
            if (File.Exists("favoritesItemOrder.json"))
            {
                File.Delete("favoritesItemOrder.json");
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
                        MainWindow.UpdateCraftDisplay(item, remove: false);
                    }
                    else if (item.CraftStatus == CraftStatus.NotStarted)
                    {
                        MainWindow.UpdateCraftDisplay(item, remove: true);
                    }
                }
            }
        }
    }
}
