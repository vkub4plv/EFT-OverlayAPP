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

namespace EFT_OverlayAPP
{
    public partial class CraftingWindow : Window, IDropTarget, INotifyPropertyChanged
    {
        public ObservableCollection<CraftableItem> CraftableItems { get; set; } = new ObservableCollection<CraftableItem>();
        public ObservableCollection<CraftableItem> FavoriteItems { get; set; } = new ObservableCollection<CraftableItem>();
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

            // Load data
            Loaded += CraftingWindow_Loaded;
        }

        private async void CraftingWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();

            // Set up views
            SetupItemsView();
            SetupFavoritesView();

            // Populate category filter
            var categories = new HashSet<string>(CraftableItems.Select(i => i.Station));
            foreach (var category in categories)
            {
                if (!string.IsNullOrEmpty(category))
                {
                    CategoryFilterComboBox.Items.Add(category);
                }
            }

            // Event handlers
            SearchTextBox.TextChanged += SearchTextBox_TextChanged;
            CategoryFilterComboBox.SelectionChanged += CategoryFilterComboBox_SelectionChanged;
            EditModeToggleButton.Checked += EditModeToggleButton_Checked;
            EditModeToggleButton.Unchecked += EditModeToggleButton_Unchecked;

            FavoritesSearchTextBox.TextChanged += FavoritesSearchTextBox_TextChanged;
            FavoritesEditModeToggleButton.Checked += EditModeToggleButton_Checked;
            FavoritesEditModeToggleButton.Unchecked += EditModeToggleButton_Unchecked;
        }

        private async Task LoadDataAsync()
        {
            LoadFavorites(); // Load favorites first

            var items = await FetchCraftableItemsAsync();
            foreach (var item in items)
            {
                // Check if item is favorited
                item.IsFavorite = favoriteIds.Contains(item.Id);
                item.PropertyChanged += Item_PropertyChanged;
                CraftableItems.Add(item);

                if (item.IsFavorite)
                {
                    FavoriteItems.Add(item);
                }
            }

            LoadItemOrder(); // Load item order after items are added
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
            FavoritesView.Filter = FavoritesFilter;

            FavoritesListView.ItemsSource = FavoritesView;
        }

        private bool ItemsFilter(object item)
        {
            var craftableItem = item as CraftableItem;
            if (craftableItem == null) return false;

            string searchText = SearchTextBox.Text.ToLower();
            string selectedCategory = CategoryFilterComboBox.SelectedItem as string;

            bool matchesSearch = string.IsNullOrEmpty(searchText) || craftableItem.RewardItems.Any(r => r.Name.ToLower().Contains(searchText));
            bool matchesCategory = selectedCategory == "All Categories" || craftableItem.Station == selectedCategory;

            return matchesSearch && matchesCategory;
        }

        private bool FavoritesFilter(object item)
        {
            var craftableItem = item as CraftableItem;
            if (craftableItem == null) return false;

            string searchText = FavoritesSearchTextBox.Text.ToLower();

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
                }
                else
                {
                    FavoriteItems.Remove(item);
                }
                SaveFavorites();
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

        // Fetch data from the GraphQL API
        public async Task<List<CraftableItem>> FetchCraftableItemsAsync()
        {
            var craftableItems = new List<CraftableItem>();
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    var queryObject = new
                    {
                        query = @"{
                            crafts {
                                id
                                station {
                                    name
                                }
                                duration
                                rewardItems {
                                    item {
                                        id
                                        name
                                        shortName
                                        iconLink
                                    }
                                    quantity
                                }
                            }
                        }"
                    };
                    var queryJson = JsonConvert.SerializeObject(queryObject);
                    var content = new StringContent(queryJson, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync("https://api.tarkov.dev/graphql", content);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();

                        // Deserialize the response
                        var graphQLResponse = JsonConvert.DeserializeObject<GraphQLCraftsResponse>(responseContent);

                        if (graphQLResponse.Data != null && graphQLResponse.Data.Crafts != null)
                        {
                            foreach (var craft in graphQLResponse.Data.Crafts)
                            {
                                var craftableItem = new CraftableItem
                                {
                                    Id = craft.Id,
                                    Station = craft.Station?.Name ?? "Unknown",
                                    CraftTime = TimeSpan.FromSeconds(craft.Duration ?? 0),
                                    RewardItems = craft.RewardItems.Select(rewardItem => new RewardItemDetail
                                    {
                                        Id = rewardItem.Item.Id,
                                        Name = rewardItem.Item.Name,
                                        ShortName = rewardItem.Item.ShortName,
                                        IconLink = rewardItem.Item.IconLink,
                                        Quantity = rewardItem.Quantity
                                    }).ToList()
                                };
                                craftableItems.Add(craftableItem);
                            }
                        }
                        else if (graphQLResponse.Errors != null && graphQLResponse.Errors.Length > 0)
                        {
                            var errorMessages = string.Join("\n", graphQLResponse.Errors.Select(e => e.Message));
                            MessageBox.Show($"GraphQL errors:\n{errorMessages}");
                        }
                        else
                        {
                            MessageBox.Show("No data received from GraphQL API.");
                        }
                    }
                    else
                    {
                        // Log the status code and reason
                        string errorContent = await response.Content.ReadAsStringAsync();
                        MessageBox.Show($"API request failed with status code {response.StatusCode}: {response.ReasonPhrase}\nContent: {errorContent}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error fetching craftable items: {ex.Message}");
                }
            }
            return craftableItems;
        }

        // Persistence for Favorites
        private List<string> favoriteIds = new List<string>();

        private void SaveFavorites()
        {
            favoriteIds = CraftableItems.Where(i => i.IsFavorite).Select(i => i.Id).ToList();
            string json = JsonConvert.SerializeObject(favoriteIds);
            File.WriteAllText("favorites.json", json);
        }

        private void LoadFavorites()
        {
            if (File.Exists("favorites.json"))
            {
                string json = File.ReadAllText("favorites.json");
                favoriteIds = JsonConvert.DeserializeObject<List<string>>(json);
            }
        }

        // Persistence for Item Order
        private void SaveItemOrder()
        {
            var itemOrder = CraftableItems.Select(i => i.Id).ToList();
            string json = JsonConvert.SerializeObject(itemOrder);
            File.WriteAllText("itemOrder.json", json);
        }

        private void LoadItemOrder()
        {
            if (File.Exists("itemOrder.json"))
            {
                string json = File.ReadAllText("itemOrder.json");
                var itemOrder = JsonConvert.DeserializeObject<List<string>>(json);

                var sortedItems = itemOrder
                    .Select(id => CraftableItems.FirstOrDefault(i => i.Id == id))
                    .Where(i => i != null)
                    .ToList();

                // Re-populate the collection to reflect the saved order
                CraftableItems.Clear();
                foreach (var item in sortedItems)
                {
                    CraftableItems.Add(item);
                }

                // Add any new items that were not in the saved order
                var newItems = CraftableItems.Where(i => !itemOrder.Contains(i.Id));
                foreach (var item in newItems)
                {
                    CraftableItems.Add(item);
                }
            }
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

            if (dropInfo.Data is CraftableItem sourceItem && dropInfo.TargetItem is CraftableItem targetItem)
            {
                var items = (ObservableCollection<CraftableItem>)dropInfo.DragInfo.SourceCollection;

                int oldIndex = items.IndexOf(sourceItem);
                int newIndex = items.IndexOf(targetItem);

                if (oldIndex != newIndex)
                {
                    items.Move(oldIndex, newIndex);
                    SaveItemOrder();
                }
            }
            else if (dropInfo.Data is CollectionViewGroup sourceGroup && dropInfo.TargetItem is CollectionViewGroup targetGroup)
            {
                // Handle moving groups (categories)
                // This requires custom logic as WPF's default grouping does not support reordering groups
                MessageBox.Show("Moving categories is not implemented in this example.");
            }
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