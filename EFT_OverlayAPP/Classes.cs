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
using System.Runtime.Serialization; // Required for StreamingContext
using System.Threading;
using System.Windows.Threading;

namespace EFT_OverlayAPP
{
    class Classes
    {
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
        public List<CraftTask> Tasks { get; set; }
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

    public class CraftableItem : INotifyPropertyChanged, IDeserializationCallback
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public string Id { get; set; } // Unique identifier
        public string Station { get; set; } // Crafting station (category)

        [JsonProperty]
        public TimeSpan CraftTime { get; set; }

        public int StationIndex { get; set; }
        public string CraftTimeString => $"{(int)CraftTime.TotalHours:D2}:{CraftTime.Minutes:D2}:{CraftTime.Seconds:D2}";

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

        [JsonIgnore]
        private bool _isDeserializing = false;

        [OnDeserializing]
        internal void OnDeserializingMethod(StreamingContext context)
        {
            _isDeserializing = true;
        }

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            _isDeserializing = false;
        }

        public void OnDeserialization(object sender)
        {
            _isDeserializing = false;
        }

        // Add properties for tracking timestamps
        [JsonProperty]
        public DateTime? CraftStartTime { get; set; } // When the craft was started

        [JsonProperty]
        public DateTime? CraftCompletedTime { get; set; } // When the craft completed (timer ran out)

        [JsonProperty]
        public DateTime? CraftFinishedTime { get; set; } // When the user finished the craft (collected)

        [JsonProperty]
        public DateTime? CraftStoppedTime { get; set; } // When the craft was stopped or replaced

        // Modify the CraftStatus property to update timestamps
        private CraftStatus craftStatus;

        [JsonProperty]
        public CraftStatus CraftStatus
        {
            get => craftStatus;
            set
            {
                if (craftStatus != value)
                {
                    var oldStatus = craftStatus;
                    craftStatus = value;
                    OnPropertyChanged(nameof(CraftStatus));
                    OnPropertyChanged(nameof(CraftButtonText));

                    if (!_isDeserializing)
                    {
                        // Update timestamps based on status transitions
                        if (oldStatus == CraftStatus.NotStarted && craftStatus == CraftStatus.InProgress)
                        {
                            // Starting a new craft
                            CraftStartTime = DateTime.Now;
                            CraftCompletedTime = null;
                            CraftStoppedTime = null;
                            CraftFinishedTime = null;
                        }
                        else if (oldStatus == CraftStatus.InProgress && craftStatus == CraftStatus.NotStarted)
                        {
                            // Stopping an active craft
                            CraftStoppedTime = DateTime.Now;
                        }
                        else if (craftStatus == CraftStatus.Ready)
                        {
                            // Craft has completed
                            CraftCompletedTime = CraftStartTime?.Add(CraftTime);
                        }
                    }

                    // Raise PropertyChanged for timestamps
                    OnPropertyChanged(nameof(CraftStartTime));
                    OnPropertyChanged(nameof(CraftCompletedTime));
                    OnPropertyChanged(nameof(CraftStoppedTime));
                    OnPropertyChanged(nameof(CraftFinishedTime));
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
                        return $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
                    else
                        return "Ready";
                }
                else if (CraftStatus == CraftStatus.Ready)
                {
                    return "Ready";
                }
                else
                {
                    return $"{(int)CraftTime.TotalHours:D2}:{CraftTime.Minutes:D2}:{CraftTime.Seconds:D2}";
                }
            }
        }

        public TimeSpan RemainingTime
        {
            get
            {
                if (CraftStatus == CraftStatus.InProgress && CraftStartTime.HasValue)
                {
                    var now = DateTime.Now;
                    var elapsed = DateTime.Now - CraftStartTime.Value;
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

        public void UpdateRemainingTime()
        {
            OnPropertyChanged(nameof(RemainingTime));
            OnPropertyChanged(nameof(RemainingTimeString));
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

    public enum CraftInstanceStatus
    {
        Started,
        Stopped,
        Completed,
        Finished
    }

    public class CraftInstance : INotifyPropertyChanged
    {
        public string Id { get; set; } // Unique identifier for the craft instance
        [JsonIgnore]
        public CraftableItem CraftableItem { get; set; } // Reference to the craftable item
        public CraftInstanceStatus Status { get; set; } // Status of the craft instance
        public DateTime StartTime { get; set; } // When the craft was started
        public DateTime? CompletedTime { get; set; } // When the craft completed (timer ran out)
        public DateTime? FinishedTime { get; set; } // When the user finished the craft (collected)
        public DateTime? StoppedTime { get; set; } // When the craft was stopped or replaced
        public int Index { get; set; } // Order in which the craft was started

        // Add properties to store IDs for serialization
        public string CraftableItemId { get; set; }
        public string Station { get; set; }

        // Computed property for conditional timestamps in Logs
        [JsonIgnore]
        public string TimestampInfo
        {
            get
            {
                switch (Status)
                {
                    case CraftInstanceStatus.Completed:
                        return $"Completed At: {CompletedTime?.ToString("MM/dd/yyyy HH:mm:ss") ?? "N/A"}, Finished At: {FinishedTime?.ToString("MM/dd/yyyy HH:mm:ss") ?? "N/A"}";
                    case CraftInstanceStatus.Stopped:
                        return $"Stopped At: {StoppedTime?.ToString("MM/dd/yyyy HH:mm:ss") ?? "N/A"}";
                    case CraftInstanceStatus.Finished:
                        return $"Finished At: {FinishedTime?.ToString("MM/dd/yyyy HH:mm:ss") ?? "N/A"}";
                    default:
                        return string.Empty;
                }
            }
        }

        [JsonIgnore]
        public string AdditionalInfo
        {
            get
            {
                if (Status == CraftInstanceStatus.Completed || Status == CraftInstanceStatus.Finished)
                {
                    return $"Completed At: {CompletedTime?.ToString() ?? "N/A"}, Finished At: {FinishedTime?.ToString() ?? "N/A"}";
                }
                else if (Status == CraftInstanceStatus.Stopped)
                {
                    return $"Stopped At: {StoppedTime?.ToString() ?? "N/A"}";
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class CraftStats : INotifyPropertyChanged
    {
        public CraftableItem CraftableItem { get; set; }
        public int TimesStarted { get; set; }
        public int TimesStopped { get; set; }
        public int TimesCompleted { get; set; }
        public DateTime? FirstStartedTime { get; set; }
        public DateTime? LastStartedTime { get; set; }
        public DateTime? LastStoppedTime { get; set; }
        public DateTime? LastCompletedTime { get; set; }


        [JsonIgnore]
        public string LastStartedTimeFormatted => LastStartedTime?.ToString() ?? "Never";
        [JsonIgnore]
        public string LastStoppedTimeFormatted => LastStoppedTime?.ToString() ?? "Never";
        [JsonIgnore]
        public string LastCompletedTimeFormatted => LastCompletedTime?.ToString() ?? "Never";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class KeybindEntry
    {
        public string Functionality { get; set; }
        public string Keybind { get; set; }
    }

    // Define the ProfileMode enum
    public enum ProfileMode
    {
        Automatic,
        Regular,
        Pve
    }

    public class AppConfig : INotifyPropertyChanged
    {
        private bool _isManualHideoutSource;
        public bool IsManualHideoutSource
        {
            get => _isManualHideoutSource;
            set
            {
                if (_isManualHideoutSource != value)
                {
                    _isManualHideoutSource = value;
                    OnPropertyChanged(nameof(IsManualHideoutSource));
                }
            }
        }

        // Property for Craft Source
        private bool _isManualCraftSource;
        public bool IsManualCraftSource
        {
            get => _isManualCraftSource;
            set
            {
                if (_isManualCraftSource != value)
                {
                    _isManualCraftSource = value;
                    OnPropertyChanged(nameof(IsManualCraftSource));
                }
            }
        }

        private string requiredItemsStartingTab;
        public string RequiredItemsStartingTab
        {
            get => requiredItemsStartingTab;
            set
            {
                if (requiredItemsStartingTab != value)
                {
                    requiredItemsStartingTab = value;
                    OnPropertyChanged(nameof(RequiredItemsStartingTab));
                }
            }
        }

        private string craftingStartingTab;
        public string CraftingStartingTab
        {
            get => craftingStartingTab;
            set
            {
                if (craftingStartingTab != value)
                {
                    craftingStartingTab = value;
                    OnPropertyChanged(nameof(CraftingStartingTab));
                }
            }
        }

        // Keybinds
        public List<KeybindEntry> Keybinds { get; set; }

        // Tarkov Tracker API
        private bool isTarkovTrackerApiEnabled;
        public bool IsTarkovTrackerApiEnabled
        {
            get => isTarkovTrackerApiEnabled;
            set
            {
                if (isTarkovTrackerApiEnabled != value)
                {
                    isTarkovTrackerApiEnabled = value;
                    OnPropertyChanged(nameof(IsTarkovTrackerApiEnabled));
                }
            }
        }

        private string selectedMapWebsite;
        public string SelectedMapWebsite
        {
            get => selectedMapWebsite;
            set
            {
                if (selectedMapWebsite != value)
                {
                    selectedMapWebsite = value;
                    OnPropertyChanged(nameof(SelectedMapWebsite));
                }
            }
        }

        private string pvpApiKey;
        public string PvpApiKey
        {
            get => pvpApiKey;
            set
            {
                if (pvpApiKey != value)
                {
                    pvpApiKey = value;
                    OnPropertyChanged(nameof(PvpApiKey));
                }
            }
        }

        private string pveApiKey;
        public string PveApiKey
        {
            get => pveApiKey;
            set
            {
                if (pveApiKey != value)
                {
                    pveApiKey = value;
                    OnPropertyChanged(nameof(PveApiKey));
                }
            }
        }

        // Toggle Visibilities
        private bool toggleMinimapVisibility;
        public bool ToggleMinimapVisibility
        {
            get => toggleMinimapVisibility;
            set
            {
                if (toggleMinimapVisibility != value)
                {
                    toggleMinimapVisibility = value;
                    OnPropertyChanged(nameof(ToggleMinimapVisibility));
                }
            }
        }

        private bool toggleRaidTimerVisibility;
        public bool ToggleRaidTimerVisibility
        {
            get => toggleRaidTimerVisibility;
            set
            {
                if (toggleRaidTimerVisibility != value)
                {
                    toggleRaidTimerVisibility = value;
                    OnPropertyChanged(nameof(ToggleRaidTimerVisibility));
                }
            }
        }

        private bool toggleCraftingTimersVisibility;
        public bool ToggleCraftingTimersVisibility
        {
            get => toggleCraftingTimersVisibility;
            set
            {
                if (toggleCraftingTimersVisibility != value)
                {
                    toggleCraftingTimersVisibility = value;
                    OnPropertyChanged(nameof(ToggleCraftingTimersVisibility));
                }
            }
        }

        private bool toggleOtherWindowButtons;
        public bool ToggleOtherWindowButtons
        {
            get => toggleOtherWindowButtons;
            set
            {
                if (toggleOtherWindowButtons != value)
                {
                    toggleOtherWindowButtons = value;
                    OnPropertyChanged(nameof(ToggleOtherWindowButtons));
                }
            }
        }

        // Paths
        private string eftLogsPath;
        public string EftLogsPath
        {
            get => eftLogsPath;
            set
            {
                if (eftLogsPath != value)
                {
                    eftLogsPath = value;
                    OnPropertyChanged(nameof(EftLogsPath));
                }
            }
        }

        private bool useCustomEftLogsPath;
        public bool UseCustomEftLogsPath
        {
            get => useCustomEftLogsPath;
            set
            {
                if (useCustomEftLogsPath != value)
                {
                    useCustomEftLogsPath = value;
                    OnPropertyChanged(nameof(UseCustomEftLogsPath));
                }
            }
        }

        // Crafting
        private int currentCraftingLevel;
        public int CurrentCraftingLevel
        {
            get => currentCraftingLevel;
            set
            {
                if (currentCraftingLevel != value)
                {
                    currentCraftingLevel = value;
                    OnPropertyChanged(nameof(CurrentCraftingLevel));
                }
            }
        }

        private bool disableAutoHideRaidTimer;
        public bool DisableAutoHideRaidTimer
        {
            get => disableAutoHideRaidTimer;
            set
            {
                if (disableAutoHideRaidTimer != value)
                {
                    disableAutoHideRaidTimer = value;
                    OnPropertyChanged(nameof(DisableAutoHideRaidTimer));
                }
            }
        }

        // Crafting

        private bool filterBasedOnHideoutLevels;
        public bool FilterBasedOnHideoutLevels
        {
            get => filterBasedOnHideoutLevels;
            set
            {
                if (filterBasedOnHideoutLevels != value)
                {
                    filterBasedOnHideoutLevels = value;
                    OnPropertyChanged(nameof(FilterBasedOnHideoutLevels));
                }
            }
        }

        private bool hideLockedQuestRecipes;
        public bool HideLockedQuestRecipes
        {
            get => hideLockedQuestRecipes;
            set
            {
                if (hideLockedQuestRecipes != value)
                {
                    hideLockedQuestRecipes = value;
                    OnPropertyChanged(nameof(HideLockedQuestRecipes));
                }
            }
        }

        // Profile Mode
        private ProfileMode selectedProfileMode;
        public ProfileMode SelectedProfileMode
        {
            get => selectedProfileMode;
            set
            {
                if (selectedProfileMode != value)
                {
                    selectedProfileMode = value;
                    OnPropertyChanged(nameof(SelectedProfileMode));
                }
            }
        }

        // Overlay Settings
        private bool autoSetActiveMinimap;
        public bool AutoSetActiveMinimap
        {
            get => autoSetActiveMinimap;
            set
            {
                if (autoSetActiveMinimap != value)
                {
                    autoSetActiveMinimap = value;
                    OnPropertyChanged(nameof(AutoSetActiveMinimap));
                }
            }
        }

        private bool hideTimerOn10MinutesLeft;
        public bool HideTimerOn10MinutesLeft
        {
            get => hideTimerOn10MinutesLeft;
            set
            {
                if (hideTimerOn10MinutesLeft != value)
                {
                    hideTimerOn10MinutesLeft = value;
                    OnPropertyChanged(nameof(HideTimerOn10MinutesLeft));
                }
            }
        }

        private bool hideTimerOnRaidEnd;
        public bool HideTimerOnRaidEnd
        {
            get => hideTimerOnRaidEnd;
            set
            {
                if (hideTimerOnRaidEnd != value)
                {
                    hideTimerOnRaidEnd = value;
                    OnPropertyChanged(nameof(HideTimerOnRaidEnd));
                }
            }
        }

        // Required Items Settings
        private bool hideItemsForBuiltStations;
        public bool HideItemsForBuiltStations
        {
            get => hideItemsForBuiltStations;
            set
            {
                if (hideItemsForBuiltStations != value)
                {
                    hideItemsForBuiltStations = value;
                    OnPropertyChanged(nameof(HideItemsForBuiltStations));
                }
            }
        }

        private bool hideItemsForCompletedQuests;
        public bool HideItemsForCompletedQuests
        {
            get => hideItemsForCompletedQuests;
            set
            {
                if (hideItemsForCompletedQuests != value)
                {
                    hideItemsForCompletedQuests = value;
                    OnPropertyChanged(nameof(HideItemsForCompletedQuests));
                }
            }
        }

        private bool hidePlantItemsMarkers;
        public bool HidePlantItemsMarkers
        {
            get => hidePlantItemsMarkers;
            set
            {
                if (hidePlantItemsMarkers != value)
                {
                    hidePlantItemsMarkers = value;
                    OnPropertyChanged(nameof(HidePlantItemsMarkers));
                }
            }
        }

        private bool hideQuestsHideoutModulesNames;
        public bool HideQuestsHideoutModulesNames
        {
            get => hideQuestsHideoutModulesNames;
            set
            {
                if (hideQuestsHideoutModulesNames != value)
                {
                    hideQuestsHideoutModulesNames = value;
                    OnPropertyChanged(nameof(HideQuestsHideoutModulesNames));
                }
            }
        }

        private bool subtractFromManualCombinedItems;
        public bool SubtractFromManualCombinedItems
        {
            get => subtractFromManualCombinedItems;
            set
            {
                if (subtractFromManualCombinedItems != value)
                {
                    subtractFromManualCombinedItems = value;
                    OnPropertyChanged(nameof(SubtractFromManualCombinedItems));
                }
            }
        }

        // Add the following property for Hideout Module Settings
        private ObservableCollection<HideoutModuleSetting> _hideoutModuleSettings;
        public ObservableCollection<HideoutModuleSetting> HideoutModuleSettings
        {
            get => _hideoutModuleSettings;
            set
            {
                if (_hideoutModuleSettings != value)
                {
                    _hideoutModuleSettings = value;
                    OnPropertyChanged(nameof(HideoutModuleSettings));
                }
            }
        }
        // Collection for Craft Module Settings
        private ObservableCollection<CraftModuleSetting> _craftModuleSettings;
        public ObservableCollection<CraftModuleSetting> CraftModuleSettings
        {
            get => _craftModuleSettings;
            set
            {
                if (_craftModuleSettings != value)
                {
                    _craftModuleSettings = value;
                    OnPropertyChanged(nameof(CraftModuleSettings));
                }
            }
        }

        public AppConfig()
        {
            // Initialize collections to prevent null references
            _craftModuleSettings = new ObservableCollection<CraftModuleSetting>();
            _hideoutModuleSettings = new ObservableCollection<HideoutModuleSetting>();
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class HideoutModuleSetting : INotifyPropertyChanged
    {
        public string ModuleName { get; set; }
        // Collection of available levels including 0 (Unbuilt)
        public ObservableCollection<int> AvailableLevels { get; set; }

        private int selectedLevel;
        public int SelectedLevel
        {
            get => selectedLevel;
            set
            {
                if (selectedLevel != value)
                {
                    selectedLevel = value;
                    OnPropertyChanged(nameof(SelectedLevel));
                }
            }
        }

        public HideoutModuleSetting()
        {
            AvailableLevels = new ObservableCollection<int>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class DebounceDispatcher
    {
        private DispatcherTimer timer;
        private readonly int intervalMilliseconds;
        private Action action;

        public DebounceDispatcher(int intervalMilliseconds = 500)
        {
            this.intervalMilliseconds = intervalMilliseconds;
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(intervalMilliseconds)
            };
            timer.Tick += Timer_Tick;
        }

        public void Debounce(Action action)
        {
            this.action = action;
            timer.Stop();
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            timer.Stop();
            action?.Invoke();
        }
    }

    public class CraftModuleSetting : INotifyPropertyChanged
    {
        private bool _isUnlocked;

        public string CraftId { get; set; } // Unique identifier for the craft
        public string CraftName { get; set; } // Name of the craft
        public string CraftIconLink { get; set; } // URL to the craft's icon
        public string TraderIconLink { get; set; } // URL to the trader's icon
        public string QuestName { get; set; } // Name of the quest that unlocks the craft

        public bool IsUnlocked
        {
            get => _isUnlocked;
            set
            {
                if (_isUnlocked != value)
                {
                    _isUnlocked = value;
                    OnPropertyChanged(nameof(IsUnlocked));
                }
            }
        }

        // Optional: Image sources for icons, loaded from URLs
        private BitmapImage _craftIcon;
        public BitmapImage CraftIcon
        {
            get
            {
                if (_craftIcon == null && !string.IsNullOrEmpty(CraftIconLink))
                {
                    _craftIcon = new BitmapImage(new System.Uri(CraftIconLink, System.UriKind.Absolute));
                }
                return _craftIcon;
            }
        }

        private BitmapImage _traderIcon;
        public BitmapImage TraderIcon
        {
            get
            {
                if (_traderIcon == null && !string.IsNullOrEmpty(TraderIconLink))
                {
                    _traderIcon = new BitmapImage(new System.Uri(TraderIconLink, System.UriKind.Absolute));
                }
                return _traderIcon;
            }
        }

        // INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class CraftTask
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public StartFinishReward StartRewards { get; set; }
        public StartFinishReward FinishRewards { get; set; }
        public Trader Trader { get; set; }
    }

    public class StartFinishReward
    {
        public List<Craft> CraftUnlock { get; set; }
    }
}
