using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel; // Added for INotifyPropertyChanged
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EFT_OverlayAPP
{
    public static class DataCache
    {
        // Define the static category order
        public static readonly List<string> StaticCategoryOrder = new List<string>
        {
            "Lavatory",
            "Workbench",
            "Medstation",
            "Nutrition Unit",
            "Intelligence Center",
            "Booze Generator",
            "Bitcoin Farm",
            "Water Collector"
        };

        // Cached data collections
        public static List<CraftableItem> CraftableItems { get; private set; } = new List<CraftableItem>();

        // Indicates whether data has been loaded
        public static bool IsDataLoaded { get; set; } = false;

        // Event to notify when data loading is complete
        public static event Action DataLoaded;

        // List to store favorite item IDs
        private static List<string> favoriteIds = new List<string>();

        // Fetch data from the GraphQL API
        public static async Task<List<CraftableItem>> FetchCraftableItemsAsync()
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
                            int index = 0;
                            foreach (var craft in graphQLResponse.Data.Crafts)
                            {
                                var craftableItem = new CraftableItem
                                {
                                    Id = craft.Id,
                                    Station = NormalizeStationName(craft.Station?.Name),
                                    CraftTime = TimeSpan.FromSeconds(craft.Duration ?? 0),
                                    RewardItems = craft.RewardItems.Select(rewardItem => new RewardItemDetail
                                    {
                                        Id = rewardItem.Item.Id,
                                        Name = rewardItem.Item.Name,
                                        ShortName = rewardItem.Item.ShortName,
                                        IconLink = rewardItem.Item.IconLink,
                                        Quantity = rewardItem.Quantity
                                    }).ToList(),
                                    OriginalIndex = index++
                                };
                                craftableItems.Add(craftableItem);
                            }

                            // Assign StationIndex based on StaticCategoryOrder
                            foreach (var item in craftableItems)
                            {
                                int idx = StaticCategoryOrder.IndexOf(item.Station);
                                item.StationIndex = idx >= 0 ? idx : StaticCategoryOrder.Count;
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

        private static string NormalizeStationName(string stationName)
        {
            return stationName?.Trim() ?? "Unknown";
        }

        public static void AddFavoriteId(string id)
        {
            if (!favoriteIds.Contains(id))
            {
                favoriteIds.Add(id);
                SaveFavorites();
            }
        }

        public static void RemoveFavoriteId(string id)
        {
            if (favoriteIds.Contains(id))
            {
                favoriteIds.Remove(id);
                SaveFavorites();
            }
        }

        private static void SaveFavorites()
        {
            string json = JsonConvert.SerializeObject(favoriteIds);
            File.WriteAllText("favorites.json", json);
        }

        private static void LoadFavorites()
        {
            if (File.Exists("favorites.json"))
            {
                string json = File.ReadAllText("favorites.json");
                favoriteIds = JsonConvert.DeserializeObject<List<string>>(json);
            }
        }

        // Methods for saving and loading favorite item order
        public static void SaveFavoriteItemOrder(IList<CraftableItem> favoriteItems)
        {
            for (int i = 0; i < favoriteItems.Count; i++)
            {
                favoriteItems[i].FavoriteSortOrder = i;
            }
            var itemOrder = favoriteItems.Select(i => i.Id).ToList();
            string json = JsonConvert.SerializeObject(itemOrder);
            File.WriteAllText("favoritesItemOrder.json", json);
        }

        public static void LoadFavoriteItemOrder(ObservableCollection<CraftableItem> favoriteItems)
        {
            if (File.Exists("favoritesItemOrder.json"))
            {
                string json = File.ReadAllText("favoritesItemOrder.json");
                var itemOrder = JsonConvert.DeserializeObject<List<string>>(json);

                int sortOrder = 0;
                foreach (var id in itemOrder)
                {
                    var item = favoriteItems.FirstOrDefault(i => i.Id == id);
                    if (item != null)
                    {
                        item.FavoriteSortOrder = sortOrder++;
                    }
                }

                // Assign FavoriteSortOrder to new items
                foreach (var item in favoriteItems)
                {
                    if (item.FavoriteSortOrder == 0 && !itemOrder.Contains(item.Id))
                    {
                        item.FavoriteSortOrder = sortOrder++;
                    }
                }
            }
        }

        public static async Task LoadDataAsync()
        {
            if (!IsDataLoaded)
            {
                LoadFavorites();

                CraftableItems = await FetchCraftableItemsAsync();

                foreach (var item in CraftableItems)
                {
                    item.IsFavorite = favoriteIds.Contains(item.Id);
                    // No need to subscribe to PropertyChanged here
                }

                // Initialize FavoriteSortOrder for favorites
                var favoriteItems = CraftableItems.Where(i => i.IsFavorite).ToList();
                foreach (var favoriteItem in favoriteItems)
                {
                    favoriteItem.FavoriteSortOrder = favoriteItems.IndexOf(favoriteItem);
                }

                // Load saved favorite item order
                LoadFavoriteItemOrder(new ObservableCollection<CraftableItem>(favoriteItems));

                IsDataLoaded = true;

                DataLoaded?.Invoke();
            }
        }

        public static void ClearData()
        {
            CraftableItems.Clear();
            IsDataLoaded = false;
        }
    }

    // Classes for deserialization remain unchanged
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

    public enum CraftStatus
    {
        NotStarted,
        InProgress,
        Ready
    }

    public class CraftableItem : INotifyPropertyChanged
    {
        public string Id { get; set; } // Unique identifier
        public string Station { get; set; } // Crafting station (category)
        public TimeSpan CraftTime { get; set; }
        public int StationIndex { get; set; }
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

        private int favoriteSortOrder;
        public int FavoriteSortOrder
        {
            get => favoriteSortOrder;
            set
            {
                favoriteSortOrder = value;
                OnPropertyChanged(nameof(FavoriteSortOrder));
            }
        }

        public int OriginalIndex { get; set; }

        public string FirstRewardItemName
        {
            get => RewardItems.FirstOrDefault()?.Name ?? string.Empty;
        }

        private CraftStatus craftStatus;
        public CraftStatus CraftStatus
        {
            get => craftStatus;
            set
            {
                if (craftStatus != value)
                {
                    craftStatus = value;
                    OnPropertyChanged(nameof(CraftStatus));
                    OnPropertyChanged(nameof(CraftButtonText));

                    // Raise PropertyChanged for RemainingTime and RemainingTimeString
                    OnPropertyChanged(nameof(RemainingTime));
                    OnPropertyChanged(nameof(RemainingTimeString));
                }
            }
        }

        private DateTime craftStartTime;
        public DateTime CraftStartTime
        {
            get => craftStartTime;
            set
            {
                if (craftStartTime != value)
                {
                    craftStartTime = value;
                    OnPropertyChanged(nameof(CraftStartTime));
                }
            }
        }

        public string RemainingTimeString
        {
            get
            {
                if (CraftStatus == CraftStatus.InProgress)
                {
                    var remaining = RemainingTime;
                    if (remaining > TimeSpan.Zero)
                        return remaining.ToString(@"hh\:mm\:ss");
                    else
                        return "Ready";
                }
                else if (CraftStatus == CraftStatus.Ready)
                {
                    return "Ready";
                }
                else
                {
                    return CraftTime.ToString(@"hh\:mm\:ss");
                }
            }
        }


        public TimeSpan RemainingTime
        {
            get
            {
                if (CraftStatus == CraftStatus.InProgress)
                {
                    var elapsed = DateTime.Now - CraftStartTime;
                    var remaining = CraftTime - elapsed;
                    return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
                }
                else if (CraftStatus == CraftStatus.Ready)
                {
                    return TimeSpan.Zero;
                }
                else
                {
                    return CraftTime;
                }
            }
        }

        public string CraftButtonText
        {
            get
            {
                switch (CraftStatus)
                {
                    case CraftStatus.NotStarted:
                        return "Start";
                    case CraftStatus.InProgress:
                        return "Stop";
                    case CraftStatus.Ready:
                        return "Finish";
                    default:
                        return "Start";
                }
            }
        }

        // Implement INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name) =>
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

    public class CraftTimerDisplayItem : INotifyPropertyChanged
    {
        public string Station { get; set; }
        public ImageSource StationIcon { get; set; }
        public CraftableItem CraftItem { get; set; }

        public string RemainingTimeString
        {
            get
            {
                return CraftItem.RemainingTimeString;
            }
        }

        // Add the RemainingTime property
        public TimeSpan RemainingTime
        {
            get
            {
                return CraftItem.RemainingTime;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RemainingTimeToColorConverter : IValueConverter
    {
        public Brush ReadyBrush { get; set; } = Brushes.Green;
        public Brush DefaultBrush { get; set; } = Brushes.White;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan remainingTime)
            {
                if (remainingTime <= TimeSpan.Zero)
                {
                    return ReadyBrush;
                }
            }
            return DefaultBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RemainingTimeToBlackOrGreenConverter : IValueConverter
    {
        public Brush ReadyBrush { get; set; } = Brushes.Green;
        public Brush DefaultBrush { get; set; } = Brushes.Black;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan remainingTime)
            {
                if (remainingTime <= TimeSpan.Zero)
                {
                    return ReadyBrush;
                }
            }
            return DefaultBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TimeSpanToStringConverter : IValueConverter
    {
        public string ReadyText { get; set; } = "Ready";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan timeSpan)
            {
                if (timeSpan <= TimeSpan.Zero)
                {
                    return ReadyText;
                }
                else
                {
                    return timeSpan.ToString(@"hh\:mm\:ss");
                }
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StationNameToIconConverter : IValueConverter
    {
        public string IconFolderPath { get; set; } = "StationIcons";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stationName)
            {
                // Build the icon path
                var iconFileName = $"{stationName}.png";
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, IconFolderPath, iconFileName);

                // Check if the file exists
                if (File.Exists(iconPath))
                {
                    return new BitmapImage(new Uri(iconPath));
                }
                else
                {
                    // Optionally, return a default icon if the specific icon is missing
                    var defaultIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, IconFolderPath, "default.png");
                    if (File.Exists(defaultIconPath))
                    {
                        return new BitmapImage(new Uri(defaultIconPath));
                    }
                }
            }
            return null; // Return null if no icon is found
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
