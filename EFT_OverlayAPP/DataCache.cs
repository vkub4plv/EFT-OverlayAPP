using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
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
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private const string ConfigFilePath = "config.json";
        private static bool SaveFavoritesWithPVE = false;
        private static bool SaveFavoriteItemOrderWithPVE = false;

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
        public static async Task<List<CraftableItem>> FetchCraftableItemsAsync(ConfigWindow ConfigWindow)
        {
            // Get or load craftable items data
            var graphQLResponse = await TarkovApiService.GetCraftableItemsDataAsync();

            var craftableItems = new List<CraftableItem>();
            if (graphQLResponse != null && graphQLResponse.Data != null && graphQLResponse.Data.Crafts != null)
            {
                int index = 0;
                foreach (var craft in graphQLResponse.Data.Crafts)
                {
                    CraftableItem craftableItem;
                    double speedReduction = (ConfigWindow.AppConfig.CurrentCraftingLevel == 51)
                        ? 0.375
                        : (ConfigWindow.AppConfig.CurrentCraftingLevel * 0.0075);

                    craftableItem = new CraftableItem
                    {
                        Id = craft.Id,
                        Station = NormalizeStationName(craft.Station?.Name),
                        CraftTime = TimeSpan.FromSeconds(craft.Duration * (1 - speedReduction) ?? 0),
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
            return craftableItems;
        }

        public static async Task<List<CraftModuleSetting>> FetchCraftModuleSettingsAsync()
        {
            // Get or load craft module settings data
            var graphQLResponse = await TarkovApiService.GetCraftModuleSettingsDataAsync();

            var craftModuleSettings = new List<CraftModuleSetting>();
            if (graphQLResponse != null && graphQLResponse.Data != null && graphQLResponse.Data.Tasks != null)
            {
                foreach (var task in graphQLResponse.Data.Tasks)
                {
                    // Process startRewards
                    if (task.StartRewards?.CraftUnlock != null)
                    {
                        foreach (var craftUnlock in task.StartRewards.CraftUnlock)
                        {
                            var craftModule = new CraftModuleSetting
                            {
                                CraftId = craftUnlock.Id,
                                CraftName = craftUnlock.RewardItems.FirstOrDefault()?.Item.Name ?? "Unknown Craft",
                                CraftIconLink = craftUnlock.RewardItems.FirstOrDefault()?.Item.IconLink ?? "",
                                TraderIconLink = task.Trader?.ImageLink ?? "",
                                QuestName = task.Name,
                                IsUnlocked = false
                            };
                            craftModuleSettings.Add(craftModule);
                        }
                    }

                    // Process finishRewards
                    if (task.FinishRewards?.CraftUnlock != null)
                    {
                        foreach (var craftUnlock in task.FinishRewards.CraftUnlock)
                        {
                            var craftModule = new CraftModuleSetting
                            {
                                CraftId = craftUnlock.Id,
                                CraftName = craftUnlock.RewardItems.FirstOrDefault()?.Item.Name ?? "Unknown Craft",
                                CraftIconLink = craftUnlock.RewardItems.FirstOrDefault()?.Item.IconLink ?? "",
                                TraderIconLink = task.Trader?.ImageLink ?? "",
                                QuestName = task.Name,
                                IsUnlocked = false
                            };
                            craftModuleSettings.Add(craftModule);
                        }
                    }
                }
            }

            return craftModuleSettings;
        }

        private static string NormalizeStationName(string stationName)
        {
            return stationName?.Trim() ?? "Unknown";
        }

        public static void AddFavoriteId(string id, ConfigWindow ConfigWindow)
        {
            if (!favoriteIds.Contains(id))
            {
                favoriteIds.Add(id);
                SaveFavorites();
            }
        }

        public static void RemoveFavoriteId(string id, ConfigWindow ConfigWindow)
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
            if (SaveFavoritesWithPVE)
            {
                File.WriteAllText("favoritesPVE.json", json);
            }
            else
            {
                File.WriteAllText("favorites.json", json);
            }
        }

        private static void LoadFavorites()
        {
            if (App.IsPVEMode)
            {
                SaveFavoritesWithPVE = true;
                if (File.Exists("favoritesPVE.json"))
                {
                    string json = File.ReadAllText("favoritesPVE.json");
                    favoriteIds = JsonConvert.DeserializeObject<List<string>>(json);
                }
            }
            else
            {

                SaveFavoritesWithPVE = false;
                if (File.Exists("favorites.json"))
                {
                    string json = File.ReadAllText("favorites.json");
                    favoriteIds = JsonConvert.DeserializeObject<List<string>>(json);
                }
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
            if (SaveFavoriteItemOrderWithPVE)
            {
                File.WriteAllText("favoritesItemOrderPVE.json", json);
            }
            else
            {
                File.WriteAllText("favoritesItemOrder.json", json);
            }
        }

        public static void LoadFavoriteItemOrder(ObservableCollection<CraftableItem> favoriteItems)
        {
            if (App.IsPVEMode)
            {
                SaveFavoriteItemOrderWithPVE = true;
                if (File.Exists("favoritesItemOrderPVE.json"))
                {
                    string json = File.ReadAllText("favoritesItemOrderPVE.json");
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
            else
            {
                SaveFavoriteItemOrderWithPVE = false;
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
        }

        public static async Task LoadDataAsync(ConfigWindow ConfigWindow)
        {
            if (!IsDataLoaded)
            {
                logger.Info("Starting data load.");

                LoadFavorites();

                // Load crafts data
                logger.Info("Loading saved crafts data.");
                var savedCrafts = CraftingDataManager.LoadCraftsData();

                logger.Info("Fetching craftable items from API.");
                CraftableItems = await FetchCraftableItemsAsync(ConfigWindow);

                logger.Info("Fetching craft module settings from API.");
                var craftModules = await FetchCraftModuleSettingsAsync();

                logger.Info("Matching saved crafts with fetched craftable items.");
                foreach (var item in CraftableItems)
                {
                    item.IsFavorite = favoriteIds.Contains(item.Id);

                    // Restore saved properties from saved crafts
                    var savedItem = savedCrafts.FirstOrDefault(c => c.Id == item.Id && c.Station == item.Station);
                    if (savedItem != null)
                    {
                        logger.Info($"Restoring saved craft for Item ID: {item.Id}, Station: {item.Station}");
                        item.CraftStatus = savedItem.CraftStatus;
                        item.CraftStartTime = savedItem.CraftStartTime;
                        item.CraftCompletedTime = savedItem.CraftCompletedTime;
                        item.CraftFinishedTime = savedItem.CraftFinishedTime;
                        item.CraftStoppedTime = savedItem.CraftStoppedTime;

                        // Check if the craft should now be marked as Ready
                        if (item.CraftStatus == CraftStatus.InProgress && item.CraftStartTime.HasValue)
                        {
                            var elapsed = DateTime.Now - item.CraftStartTime.Value; // Use UtcNow
                            if (elapsed >= item.CraftTime)
                            {
                                // Craft has completed while the app was closed
                                item.CraftStatus = CraftStatus.Ready;
                                item.CraftCompletedTime = item.CraftStartTime.Value.Add(item.CraftTime);
                                item.OnPropertyChanged(nameof(item.CraftStatus));
                                item.OnPropertyChanged(nameof(item.CraftCompletedTime));
                            }
                        }
                    }
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

                logger.Info("Data load completed.");

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
                return;

            var responseObject = await TarkovApiService.GetRequiredItemsDataAsync();
            if (responseObject == null)
            {
                // Already handled errors in the service call
                return;
            }

            JObject data = responseObject["data"] as JObject;
            if (data != null)
            {
                ParseTasks(data["tasks"] as JArray);
                ParseHideoutStations(data["hideoutStations"] as JArray);
                IsRequiredItemsDataLoaded = true;
            }
            else
            {
                MessageBox.Show("API returned null data.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
}
