using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel; // Added for INotifyPropertyChanged
using System.Diagnostics;
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

        // Flag to indicate whether the data has already been loaded
        public static bool IsRequiredItemsDataLoaded { get; private set; } = false;

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

        public static List<Quest> Quests { get; set; } = new List<Quest>();
        public static List<HideoutStation> HideoutStations { get; set; } = new List<HideoutStation>();

        public static async Task LoadRequiredItemsData()
        {
            if (IsRequiredItemsDataLoaded)
                return; // Data is already loaded, no need to load again

            string query = @"
            {
              tasks {
                id
                name
                trader {
                  id
                  name
                  imageLink
                }
                objectives {
                  id
                  type
                  description
                  ... on TaskObjectiveItem {
                    items {
                      id
                      name
                      iconLink
                    }
                    count
                    foundInRaid
                  }
                }
              }
              hideoutStations {
                id
                name
                normalizedName
                imageLink
                levels {
                  level
                  itemRequirements {
                    item {
                      id
                      name
                      iconLink 
                    }
                    count
                  }
                }
              }
            }";


            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // Create an anonymous object for the query
                    var queryObject = new { query = query };
                    var jsonContent = JsonConvert.SerializeObject(queryObject);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync("https://api.tarkov.dev/graphql", content);

                    string result = await response.Content.ReadAsStringAsync();

                    // Log the response
                    // MessageBox.Show(result, "API Response", MessageBoxButton.OK, MessageBoxImage.Information);

                    JObject responseObject = JObject.Parse(result);

                    // Check for errors in the response
                    if (responseObject["errors"] != null)
                    {
                        var errors = responseObject["errors"].ToString();
                        MessageBox.Show($"API returned errors: {errors}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    JObject data = responseObject["data"] as JObject;
                    if (data != null)
                    {
                        ParseTasks(data["tasks"] as JArray);
                        ParseHideoutStations(data["hideoutStations"] as JArray);
                        IsRequiredItemsDataLoaded = true; // Set the flag after successful loading
                    }
                    else
                    {
                        // Handle the case where 'data' is null
                        MessageBox.Show("API returned null data.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading required items data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private static void ParseTasks(JArray tasksArray)
        {
            Quests.Clear();
            foreach (var taskToken in tasksArray)
            {
                var quest = new Quest
                {
                    Id = taskToken["id"].ToString(),
                    Name = taskToken["name"].ToString(),
                    Trader = new Trader
                    {
                        Id = taskToken["trader"]["id"].ToString(),
                        Name = taskToken["trader"]["name"].ToString(),
                        ImageLink = taskToken["trader"]["imageLink"].ToString()
                    },
                    Objectives = new List<QuestObjective>()
                };

                var objectivesArray = taskToken["objectives"] as JArray;
                foreach (var objectiveToken in objectivesArray)
                {
                    string type = objectiveToken["type"].ToString();
                    if (type == "giveItem" || type == "plantItem")
                    {
                        var itemsArray = objectiveToken["items"] as JArray;
                        if (itemsArray != null)
                        {
                            var objective = new QuestObjective
                            {
                                Id = objectiveToken["id"].ToString(),
                                Type = type,
                                Description = objectiveToken["description"].ToString(),
                                Items = new List<Item>(),
                                Count = (int)objectiveToken["count"],
                                FoundInRaid = (bool)objectiveToken["foundInRaid"]
                            };

                            foreach (var itemToken in itemsArray)
                            {
                                var item = new Item
                                {
                                    Id = itemToken["id"].ToString(),
                                    Name = itemToken["name"].ToString(),
                                    IconLink = itemToken["iconLink"].ToString()
                                };
                                objective.Items.Add(item);
                            }

                            quest.Objectives.Add(objective);
                        }
                    }
                }

                if (quest.Objectives.Any())
                {
                    Quests.Add(quest);
                }
            }
        }

        private static void ParseHideoutStations(JArray stationsArray)
        {
            HideoutStations.Clear();
            foreach (var stationToken in stationsArray)
            {
                var station = new HideoutStation
                {
                    Id = stationToken["id"].ToString(),
                    Name = stationToken["name"].ToString(),
                    NormalizedName = stationToken["normalizedName"].ToString(),
                    ImageLink = stationToken["imageLink"].ToString(),
                    Levels = new List<HideoutStationLevel>()
                };

                var levelsArray = stationToken["levels"] as JArray;
                foreach (var levelToken in levelsArray)
                {
                    var level = new HideoutStationLevel
                    {
                        Level = (int)levelToken["level"],
                        ItemRequirements = new List<ItemRequirement>()
                    };

                    var itemRequirementsArray = levelToken["itemRequirements"] as JArray;
                    foreach (var reqToken in itemRequirementsArray)
                    {
                        var itemToken = reqToken["item"];
                        var item = new Item
                        {
                            Id = itemToken["id"].ToString(),
                            Name = itemToken["name"].ToString(),
                            IconLink = itemToken["iconLink"].ToString()
                        };

                        var itemRequirement = new ItemRequirement
                        {
                            Item = item,
                            Count = (int)reqToken["count"]
                        };

                        level.ItemRequirements.Add(itemRequirement);
                    }

                    station.Levels.Add(level);
                }

                if (station.Levels.Any())
                {
                    HideoutStations.Add(station);
                }
            }
        }
    }

    // Model Classes
    public class Quest
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Trader Trader { get; set; }
        public List<QuestObjective> Objectives { get; set; }
    }

    public class Trader
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ImageLink { get; set; }
    }

    public class QuestObjective
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public List<Item> Items { get; set; }
        public int Count { get; set; }
        public bool FoundInRaid { get; set; }
    }

    public class HideoutStation
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string NormalizedName { get; set; }
        public string ImageLink { get; set; }
        public List<HideoutStationLevel> Levels { get; set; }
    }

    public class HideoutStationLevel
    {
        public int Level { get; set; }
        public List<ItemRequirement> ItemRequirements { get; set; }
    }

    public class ItemRequirement
    {
        public Item Item { get; set; }
        public int Count { get; set; }
    }

    public class Item
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string IconLink { get; set; }
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

    public class CompletionToBackgroundConverter : IValueConverter
    {
        public Brush CompletedBrush { get; set; } = Brushes.LightGreen;
        public Brush DefaultBrush { get; set; } = Brushes.Transparent;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isComplete)
            {
                return isComplete ? CompletedBrush : DefaultBrush;
            }
            return DefaultBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CompletionToForegroundConverter : IValueConverter
    {
        public Brush CompletedBrush { get; set; } = Brushes.Green;
        public Brush DefaultBrush { get; set; } = Brushes.Black;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isComplete)
            {
                return isComplete ? CompletedBrush : DefaultBrush;
            }
            return DefaultBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RequiredItemQuantity
    {
        public string Id { get; set; }
        public int QuantityOwned { get; set; }
    }

    public class CombinedRequiredItemEntry : INotifyPropertyChanged
    {
        public Item Item { get; set; }
        private int quantityNeeded;
        public int QuantityNeeded
        {
            get => quantityNeeded;
            set
            {
                if (quantityNeeded != value)
                {
                    quantityNeeded = value;
                    OnPropertyChanged(nameof(QuantityNeeded));
                    OnPropertyChanged(nameof(QuantityOwnedNeeded));
                    OnPropertyChanged(nameof(IsComplete));
                }
            }
        }
        private int quantityOwned;
        public int QuantityOwned
        {
            get => quantityOwned;
            set
            {
                if (quantityOwned != value)
                {
                    quantityOwned = value;
                    OnPropertyChanged(nameof(QuantityOwned));
                    OnPropertyChanged(nameof(QuantityOwnedNeeded));
                    OnPropertyChanged(nameof(IsComplete));
                }
            }
        }
        public string QuantityOwnedNeeded => $"{QuantityOwned} / {QuantityNeeded}";
        public bool IsFoundInRaid { get; set; }
        // New property to hold list of source details
        public List<SourceDetail> RequiredForDetails { get; set; } = new List<SourceDetail>();

        public bool IsComplete => QuantityOwned >= QuantityNeeded;
        // New property to hold source icons
        public List<string> SourceIcons { get; set; } = new List<string>();

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RequiredItemEntry : INotifyPropertyChanged
    {
        public string Id { get; set; }
        public Item Item { get; set; }
        private int quantityNeeded;
        public int QuantityNeeded
        {
            get => quantityNeeded;
            set
            {
                if (quantityNeeded != value)
                {
                    quantityNeeded = value;
                    OnPropertyChanged(nameof(QuantityNeeded));
                    OnPropertyChanged(nameof(QuantityOwnedNeeded));
                    OnPropertyChanged(nameof(IsComplete));
                }
            }
        }
        private int quantityOwned;
        public int QuantityOwned
        {
            get => quantityOwned;
            set
            {
                if (quantityOwned != value)
                {
                    quantityOwned = value;
                    OnPropertyChanged(nameof(QuantityOwned));
                    OnPropertyChanged(nameof(QuantityOwnedNeeded));
                    OnPropertyChanged(nameof(IsComplete));
                }
            }
        }
        public string QuantityOwnedNeeded => $"{QuantityOwned} / {QuantityNeeded}";
        public bool IsFoundInRaid { get; set; }
        public string SourceIcon { get; set; }
        public string SourceName { get; set; }
        public string SourceDetail { get; set; }
        public string GroupType { get; set; } // "Quests" or "Hideout"
        public bool IsComplete
        {
            get
            {
                if (ParentEntry != null)
                {
                    return ParentEntry.QuantityOwned >= ParentEntry.QuantityNeeded;
                }
                else if (IsCombined)
                {
                    return QuantityOwned >= QuantityNeeded;
                }
                else
                {
                    return QuantityOwned >= QuantityNeeded;
                }
            }
        }

        // For combined entries
        public bool IsCombined { get; set; }
        public RequiredItemEntry ParentEntry { get; set; }
        public List<RequiredItemEntry> ChildEntries { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class SourceDetail
    {
        public string Icon { get; set; } // URL or path to the icon image
        public string Name { get; set; } // Quest name or hideout level
    }

    public class BooleanToStringConverter : IValueConverter
    {
        // ConverterParameter format: "TrueValue,FalseValue"
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string param && value is bool boolValue)
            {
                var values = param.Split(',');
                if (values.Length == 2)
                {
                    return boolValue ? values[0].Trim() : values[1].Trim();
                }
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
