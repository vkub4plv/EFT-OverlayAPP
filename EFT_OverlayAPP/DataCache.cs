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

        // Event to notify when data loading is complete
        public static event Action DataLoaded;

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
                    bool shouldBeLocked = false;

                    var matchingEntry = ConfigWindow.AppConfig.EffectiveCraftModuleSettings
                        .FirstOrDefault(entry => entry.CraftId == craft.Id);

                    if (matchingEntry != null && !matchingEntry.IsUnlocked)
                    {
                        shouldBeLocked = true;
                    }

                    double speedReduction;
                    if (App.IsPVEMode)
                    {
                        speedReduction = (ConfigWindow.AppConfig.CurrentCraftingLevelPVE == 51)
                            ? 0.375
                            : (ConfigWindow.AppConfig.CurrentCraftingLevelPVE * 0.0075);
                    }
                    else
                    {
                        speedReduction = (ConfigWindow.AppConfig.CurrentCraftingLevel == 51)
                            ? 0.375
                            : (ConfigWindow.AppConfig.CurrentCraftingLevel * 0.0075);
                    }

                    craftableItem = new CraftableItem
                    {
                        Id = craft.Id,
                        Station = NormalizeStationName(craft.Station?.Name),
                        StationId = craft.Station.Id,
                        StationLevel = craft?.Level ?? 1,
                        IsLocked = shouldBeLocked,
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
                                QuestId = task.Id,
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
                                QuestId = task.Id,
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

            logger.Info("Data load completed.");

            DataLoaded?.Invoke();
        }

        public static List<Quest> Quests { get; set; } = new List<Quest>();
        public static List<HideoutStation> HideoutStations { get; set; } = new List<HideoutStation>();

        public static async Task LoadRequiredItemsData()
        {

            var graphQLResponse = await TarkovApiService.GetRequiredItemsDataAsync();
            if (graphQLResponse == null || graphQLResponse.Data == null)
            {
                // Error handling is done in the service. Just return here.
                return;
            }

            // Clear lists before re-populating
            Quests.Clear();
            HideoutStations.Clear();

            // Parse tasks into Quests
            foreach (var taskInfo in graphQLResponse.Data.Tasks)
            {
                var quest = new Quest
                {
                    Id = taskInfo.Id,
                    Name = taskInfo.Name,
                    Trader = new Trader
                    {
                        Id = taskInfo.Trader.Id,
                        Name = taskInfo.Trader.Name,
                        ImageLink = taskInfo.Trader.ImageLink
                    },
                    Objectives = new List<QuestObjective>()
                };

                if (taskInfo.Objectives != null)
                {
                    foreach (var objectiveInfo in taskInfo.Objectives)
                    {
                        // We only care about 'giveItem' or 'plantItem'
                        if (objectiveInfo.Type == "giveItem" || objectiveInfo.Type == "plantItem")
                        {
                            var objective = new QuestObjective
                            {
                                Id = objectiveInfo.Id,
                                Type = objectiveInfo.Type,
                                Description = objectiveInfo.Description,
                                Count = objectiveInfo.Count,
                                FoundInRaid = objectiveInfo.FoundInRaid,
                                Items = new List<Item>()
                            };

                            if (objectiveInfo.Items != null)
                            {
                                foreach (var itemInfo in objectiveInfo.Items)
                                {
                                    objective.Items.Add(new Item
                                    {
                                        Id = itemInfo.Id,
                                        Name = itemInfo.Name,
                                        IconLink = itemInfo.IconLink
                                    });
                                }
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

            // Parse hideout stations
            foreach (var stationInfo in graphQLResponse.Data.HideoutStations)
            {
                var station = new HideoutStation
                {
                    Id = stationInfo.Id,
                    Name = stationInfo.Name,
                    NormalizedName = stationInfo.NormalizedName,
                    ImageLink = stationInfo.ImageLink,
                    Levels = new List<HideoutStationLevel>()
                };

                if (stationInfo.Levels != null)
                {
                    foreach (var levelInfo in stationInfo.Levels)
                    {
                        var level = new HideoutStationLevel
                        {
                            Level = levelInfo.Level,
                            ItemRequirements = new List<ItemRequirement>()
                        };

                        if (levelInfo.ItemRequirements != null)
                        {
                            foreach (var reqInfo in levelInfo.ItemRequirements)
                            {
                                level.ItemRequirements.Add(new ItemRequirement
                                {
                                    Item = new Item
                                    {
                                        Id = reqInfo.Item.Id,
                                        Name = reqInfo.Item.Name,
                                        IconLink = reqInfo.Item.IconLink
                                    },
                                    Count = reqInfo.Count
                                });
                            }
                        }

                        station.Levels.Add(level);
                    }
                }

                if (station.Levels.Any())
                {
                    HideoutStations.Add(station);
                }
            }
        }
    }
}
