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

namespace EFT_OverlayAPP
{
    public partial class CraftingWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<CraftableItem> CraftableItems { get; set; }
        public ObservableCollection<CraftableItem> FavoriteItems { get; set; }
        public ICollectionView ItemsView { get; set; }
        public ICollectionView FavoritesView { get; set; }

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

        public CraftingWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Initialize collections
            CraftableItems = new ObservableCollection<CraftableItem>();
            FavoriteItems = new ObservableCollection<CraftableItem>();

            // Subscribe to CollectionChanged event
            FavoriteItems.CollectionChanged += FavoriteItems_CollectionChanged;

            // Subscribe to the DataLoaded event
            DataCache.DataLoaded += OnDataLoaded;

            // Check if data is already loaded
            if (DataCache.IsDataLoaded)
            {
                InitializeData();
            }
            else
            {
                IsLoading = true;
                Task.Run(() => DataCache.LoadDataAsync());
            }
        }

        private void OnDataLoaded()
        {
            Dispatcher.Invoke(() =>
            {
                InitializeData();
                IsLoading = false;
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
            PopulateCategoryFilter();
            PopulateFavoritesCategoryFilter();

            // Refresh views
            ItemsView?.Refresh();
            FavoritesView?.Refresh();

            // Event handlers
            SearchTextBox.TextChanged += SearchTextBox_TextChanged;
            CategoryFilterComboBox.SelectionChanged += CategoryFilterComboBox_SelectionChanged;

            FavoritesSearchTextBox.TextChanged += FavoritesSearchTextBox_TextChanged;
            FavoritesCategoryFilterComboBox.SelectionChanged += FavoritesCategoryFilterComboBox_SelectionChanged;
        }

        private void SetupItemsView()
        {
            ItemsView = CollectionViewSource.GetDefaultView(CraftableItems);

            // Clear existing group descriptions
            ItemsView.GroupDescriptions.Clear();

            // Add new group description
            ItemsView.GroupDescriptions.Add(new PropertyGroupDescription("Station"));

            ItemsView.Filter = ItemsFilter;

            ItemListView.ItemsSource = ItemsView;
        }

        private void SetupFavoritesView()
        {
            FavoritesView = CollectionViewSource.GetDefaultView(FavoriteItems);

            // Clear existing group descriptions
            FavoritesView.GroupDescriptions.Clear();

            // Add new group description
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

            // Add categories to the ComboBox
            foreach (var category in categories)
            {
                if (!string.IsNullOrEmpty(category))
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

            // Add categories to the ComboBox
            foreach (var category in categories)
            {
                if (!string.IsNullOrEmpty(category))
                {
                    FavoritesCategoryFilterComboBox.Items.Add(category);
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

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CraftableItem.IsFavorite))
            {
                var item = sender as CraftableItem;

                // Avoid duplicate entries in FavoriteItems
                if (item.IsFavorite && !FavoriteItems.Contains(item))
                {
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
            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                DataCache.SaveFavoriteItemOrder(FavoriteItems);
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is CraftableItem item)
            {
                // Handle the start action
                StartCrafting(item);
            }
        }

        private void StartCrafting(CraftableItem item)
        {
            // Implement the logic to handle the start of crafting
            MessageBox.Show($"Starting crafting of {string.Join(", ", item.RewardItems.Select(r => r.Name))}");
            // Later, you can implement timers or other functionality here
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
                int targetIdx = FavoriteItems.Count;

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

                if (removedIdx >= 0 && targetIdx >= 0)
                {
                    if (removedIdx != targetIdx)
                    {
                        FavoriteItems.Move(removedIdx, targetIdx);
                        DataCache.SaveFavoriteItemOrder(FavoriteItems);
                    }
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

            // Re-populate FavoriteItems based on current favorites in default order
            foreach (var item in CraftableItems)
            {
                if (item.IsFavorite)
                {
                    FavoriteItems.Add(item);
                }
            }

            // Refresh the favorites view
            FavoritesView.Refresh();

            IsLoading = false; // Hide loading indicator
        }

    }

    // Classes for deserialization
    public class GraphQLCraftsResponse
    {
        public CraftsData Data { get; set; }
        public GraphQLError[] Errors { get; set; }
    }

    public class CraftsData
    {
        public List<Craft> Crafts { get; set; }
    }

    public class Craft
    {
        public string Id { get; set; }
        public Station Station { get; set; }
        public int? Duration { get; set; }
        public List<RewardItem> RewardItems { get; set; }
    }

    public class Station
    {
        public string Name { get; set; }
    }

    public class RewardItem
    {
        public RewardItemDetail Item { get; set; }
        public int Quantity { get; set; }
    }

    public class GraphQLError
    {
        public string Message { get; set; }
    }

    public class CraftableItem : INotifyPropertyChanged
    {
        public string Id { get; set; } // Unique identifier
        public string Station { get; set; } // Crafting station (category)
        public TimeSpan CraftTime { get; set; }
        public string CraftTimeString => CraftTime.ToString(@"hh\:mm\:ss");

        public List<RewardItemDetail> RewardItems { get; set; } // List of reward items

        private bool isFavorite;
        public bool IsFavorite
        {
            get => isFavorite;
            set
            {
                isFavorite = value;
                OnPropertyChanged(nameof(IsFavorite));
            }
        }

        // Implement INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RewardItemDetail
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string IconLink { get; set; }
        public int Quantity { get; set; }
    }
}
