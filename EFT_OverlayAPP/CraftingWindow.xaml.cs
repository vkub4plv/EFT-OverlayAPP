using EFT_OverlayAPP;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using GongSolutions.Wpf.DragDrop;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Collections;

namespace EFT_OverlayAPP
{
    public partial class CraftingWindow : Window, IDropTarget, INotifyPropertyChanged
    {
        public ObservableCollection<CraftableItem> CraftableItems { get; set; }
        public ObservableCollection<CraftableItem> FavoriteItems { get; set; }
        public ICollectionView ItemsView { get; set; }
        public ICollectionView FavoritesView { get; set; }

        private bool isEditMode;
        public bool IsEditMode
        {
            get => isEditMode;
            set
            {
                isEditMode = value;
                OnPropertyChanged(nameof(IsEditMode));
            }
        }

        public CraftingWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Initialize collections
            CraftableItems = new ObservableCollection<CraftableItem>();
            FavoriteItems = new ObservableCollection<CraftableItem>();

            // Subscribe to the DataLoaded event
            DataCache.DataLoaded += OnDataLoaded;

            // Check if data is already loaded
            if (DataCache.IsDataLoaded)
            {
                // Data is already loaded, initialize the UI
                InitializeData();
            }
            else
            {
                // Start data loading (if not already started)
                Task.Run(() => DataCache.LoadDataAsync());
            }
        }

        private void OnDataLoaded()
        {
            Dispatcher.Invoke(() =>
            {
                InitializeData();
                // Optionally, hide loading indicator here if implemented
            });
        }

        private void InitializeData()
        {
            // Clear existing items
            CraftableItems.Clear();
            FavoriteItems.Clear();

            // Populate the observable collections with cached data
            foreach (var item in DataCache.CraftableItems)
            {
                // Subscribe to PropertyChanged event
                item.PropertyChanged += Item_PropertyChanged;

                CraftableItems.Add(item);

                if (item.IsFavorite)
                {
                    FavoriteItems.Add(item);
                }
            }

            // Set up views and filters
            SetupItemsView();
            SetupFavoritesView();
            PopulateCategoryFilter();
            // If you have a favorites category filter, call PopulateFavoritesCategoryFilter();

            // Refresh views
            ItemsView?.Refresh();
            FavoritesView?.Refresh();

            // Event handlers
            SearchTextBox.TextChanged += SearchTextBox_TextChanged;
            CategoryFilterComboBox.SelectionChanged += CategoryFilterComboBox_SelectionChanged;
            EditModeToggleButton.Checked += EditModeToggleButton_Checked;
            EditModeToggleButton.Unchecked += EditModeToggleButton_Unchecked;

            FavoritesSearchTextBox.TextChanged += FavoritesSearchTextBox_TextChanged;
            FavoritesEditModeToggleButton.Checked += EditModeToggleButton_Checked;
            FavoritesEditModeToggleButton.Unchecked += EditModeToggleButton_Unchecked;
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

        private void SetupItemsView()
        {
            ItemsView = CollectionViewSource.GetDefaultView(CraftableItems);
            ItemsView.GroupDescriptions.Add(new PropertyGroupDescription("Station"));
            ItemsView.Filter = ItemsFilter;

            ItemListView.ItemsSource = ItemsView;
        }

        private void SetupFavoritesView()
        {
            FavoritesView = CollectionViewSource.GetDefaultView(FavoriteItems);
            FavoritesView.GroupDescriptions.Add(new PropertyGroupDescription("Station"));
            FavoritesView.Filter = FavoritesFilter;

            FavoritesListView.ItemsSource = FavoritesView;
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

            bool matchesSearch = string.IsNullOrEmpty(searchText) || craftableItem.RewardItems.Any(r => r.Name.ToLower().Contains(searchText));

            return craftableItem.IsFavorite && matchesSearch;
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

        private void EditModeToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            IsEditMode = true;
        }

        private void EditModeToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            IsEditMode = false;
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CraftableItem.IsFavorite))
            {
                var item = sender as CraftableItem;
                if (item.IsFavorite)
                {
                    FavoriteItems.Add(item);
                    DataCache.AddFavoriteId(item.Id);
                }
                else
                {
                    FavoriteItems.Remove(item);
                    DataCache.RemoveFavoriteId(item.Id);
                }
            }
        }

        private async void ResetOrderButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists("itemOrder.json"))
            {
                File.Delete("itemOrder.json");
            }

            // Clear data in DataCache
            DataCache.ClearData();
            DataCache.IsDataLoaded = false;

            // Start data loading again
            await DataCache.LoadDataAsync();

            // Update the observable collections
            InitializeData();
        }

        private async void FavoritesResetOrderButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists("favoritesItemOrder.json"))
            {
                File.Delete("favoritesItemOrder.json");
            }

            // Clear data in DataCache
            DataCache.ClearData();
            DataCache.IsDataLoaded = false;

            // Start data loading again
            await DataCache.LoadDataAsync();

            // Update the observable collections
            InitializeData();
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

        // Implement IDropTarget for Drag-and-Drop
        public void DragOver(IDropInfo dropInfo)
        {
            if (!IsEditMode)
            {
                dropInfo.Effects = DragDropEffects.None;
                return;
            }

            if (dropInfo.Data is CraftableItem && dropInfo.TargetItem is CraftableItem)
            {
                dropInfo.Effects = DragDropEffects.Move;
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
            }
            else if (dropInfo.Data is CollectionViewGroup && dropInfo.TargetItem is CollectionViewGroup)
            {
                // Handle moving groups (categories) - Not implemented
                dropInfo.Effects = DragDropEffects.None;
            }
            else
            {
                dropInfo.Effects = DragDropEffects.None;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            if (!IsEditMode)
            {
                return;
            }

            if (dropInfo.Data is CraftableItem sourceItem)
            {
                // Get the source and target collections
                IList sourceCollection = GetUnderlyingCollection(dropInfo.DragInfo.SourceCollection);
                IList targetCollection = GetUnderlyingCollection(dropInfo.TargetCollection);

                if (sourceCollection == null || targetCollection == null)
                {
                    MessageBox.Show("Unable to reorder items in this view.");
                    return;
                }

                // Remove the item from the source collection
                int oldIndex = sourceCollection.IndexOf(sourceItem);
                if (oldIndex >= 0)
                {
                    sourceCollection.RemoveAt(oldIndex);
                }

                // Determine the index to insert in the target collection
                int insertIndex = dropInfo.InsertIndex;
                if (targetCollection == sourceCollection && oldIndex < insertIndex)
                {
                    insertIndex--;
                }

                // Insert the item into the target collection
                if (insertIndex >= 0)
                {
                    targetCollection.Insert(insertIndex, sourceItem);
                }
                else
                {
                    targetCollection.Add(sourceItem);
                }

                // Save the new order
                DataCache.SaveItemOrder();
            }
            else if (dropInfo.Data is CollectionViewGroup)
            {
                MessageBox.Show("Moving categories is not implemented in this example.");
            }
        }

        private IList GetUnderlyingCollection(object collection)
        {
            if (collection is CollectionViewGroup group)
            {
                return group.Items;
            }
            else if (collection is ICollectionView view)
            {
                return view.SourceCollection as IList;
            }
            else
            {
                return collection as IList;
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
