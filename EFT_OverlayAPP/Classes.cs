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
using Newtonsoft.Json.Converters;
using Refit;
using System.Net;

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
        public int Level { get; set; }
        public Station Station { get; set; }
        public int? Duration { get; set; }
        public List<RewardItem> RewardItems { get; set; }
    }

    public class Station
    {
        public string Id { get; set; }
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
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        public string Id { get; set; } // Unique identifier
        public string Station { get; set; } // Crafting station (category)
        public string StationId { get; set; } // Crafting station ID
        public int StationLevel { get; set; } // Crafting station level
        private bool isLocked = false;
        public bool IsLocked
        {
            get => isLocked;
            set
            {
                isLocked = value;
                OnPropertyChanged(nameof(IsLocked));
            }
        }

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

        private ImageSource _displaySourceIcon;
        public ImageSource DisplaySourceIcon
        {
            get
            {
                if (_displaySourceIcon == null)
                {
                    // Check if we need to use a local override
                    if (SourceName.Equals("Cultist Circle", StringComparison.OrdinalIgnoreCase) ||
                        SourceName.Equals("Gear Rack", StringComparison.OrdinalIgnoreCase))
                    {
                        // Use local icon from /StationIcons
                        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        string iconFileName = SourceName.Equals("Cultist Circle", StringComparison.OrdinalIgnoreCase)
                            ? "Cultist Circle.png"
                            : "Gear Rack.png";

                        string localIconPath = Path.Combine(baseDir, "StationIcons", iconFileName);
                        if (File.Exists(localIconPath))
                        {
                            _displaySourceIcon = new BitmapImage(new Uri(localIconPath, UriKind.Absolute));
                        }
                        else
                        {
                            // Fallback to a default icon if missing
                            string defaultIconPath = Path.Combine(baseDir, "StationIcons", "default.png");
                            if (File.Exists(defaultIconPath))
                            {
                                _displaySourceIcon = new BitmapImage(new Uri(defaultIconPath, UriKind.Absolute));
                            }
                        }
                    }
                    else
                    {
                        // Use the API-provided icon
                        if (!string.IsNullOrEmpty(SourceIcon))
                        {
                            try
                            {
                                _displaySourceIcon = new BitmapImage(new Uri(SourceIcon, UriKind.Absolute));
                            }
                            catch
                            {
                                // If failed to load from URL, fallback to default
                                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                                string defaultIconPath = Path.Combine(baseDir, "StationIcons", "default.png");
                                if (File.Exists(defaultIconPath))
                                {
                                    _displaySourceIcon = new BitmapImage(new Uri(defaultIconPath, UriKind.Absolute));
                                }
                            }
                        }
                        else
                        {
                            // If SourceIcon is empty, fallback to default
                            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                            string defaultIconPath = Path.Combine(baseDir, "StationIcons", "default.png");
                            if (File.Exists(defaultIconPath))
                            {
                                _displaySourceIcon = new BitmapImage(new Uri(defaultIconPath, UriKind.Absolute));
                            }
                        }
                    }
                }
                return _displaySourceIcon;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class SourceDetail : INotifyPropertyChanged
    {
        public string Icon { get; set; } // URL or path to the icon image
        public string Name { get; set; } // Quest name or hideout level

        private ImageSource _displayIcon;
        public ImageSource DisplayIcon
        {
            get
            {
                if (_displayIcon == null)
                {
                    // Check for special stations
                    if (Name.Contains("Cultist Circle", StringComparison.OrdinalIgnoreCase) ||
                        Name.Contains("Gear Rack", StringComparison.OrdinalIgnoreCase))
                    {
                        // Use local icon
                        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        string iconFileName = Name.Contains("Cultist Circle", StringComparison.OrdinalIgnoreCase)
                            ? "Cultist Circle.png"
                            : "Gear Rack.png";

                        string localIconPath = Path.Combine(baseDir, "StationIcons", iconFileName);
                        if (File.Exists(localIconPath))
                        {
                            _displayIcon = new BitmapImage(new Uri(localIconPath, UriKind.Absolute));
                        }
                        else
                        {
                            // Fallback to default if missing
                            string defaultIconPath = Path.Combine(baseDir, "StationIcons", "default.png");
                            if (File.Exists(defaultIconPath))
                            {
                                _displayIcon = new BitmapImage(new Uri(defaultIconPath, UriKind.Absolute));
                            }
                        }
                    }
                    else
                    {
                        // Use API-provided icon if available
                        if (!string.IsNullOrEmpty(Icon))
                        {
                            try
                            {
                                _displayIcon = new BitmapImage(new Uri(Icon, UriKind.Absolute));
                            }
                            catch
                            {
                                // If failed, fallback to default
                                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                                string defaultIconPath = Path.Combine(baseDir, "StationIcons", "default.png");
                                if (File.Exists(defaultIconPath))
                                {
                                    _displayIcon = new BitmapImage(new Uri(defaultIconPath, UriKind.Absolute));
                                }
                            }
                        }
                        else
                        {
                            // If no icon URL, use default
                            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                            string defaultIconPath = Path.Combine(baseDir, "StationIcons", "default.png");
                            if (File.Exists(defaultIconPath))
                            {
                                _displayIcon = new BitmapImage(new Uri(defaultIconPath, UriKind.Absolute));
                            }
                        }
                    }
                }
                return _displayIcon;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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

    public class KeybindEntry : INotifyPropertyChanged
    {
        private string functionality;
        public string Functionality
        {
            get => functionality;
            set
            {
                if (functionality != value)
                {
                    functionality = value;
                    OnPropertyChanged(nameof(Functionality));
                }
            }
        }

        private string keybind;
        public string Keybind
        {
            get => keybind;
            set
            {
                if (keybind != value)
                {
                    keybind = value;
                    OnPropertyChanged(nameof(Keybind));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
        // Define file name templates
        public string FavoritesFileName => EffectiveProfileMode == ProfileMode.Pve ? "favoritesPVE.json" : "favorites.json";
        public string FavoritesItemOrderFileName => EffectiveProfileMode == ProfileMode.Pve ? "favoritesItemOrderPVE.json" : "favoritesItemOrder.json";
        public string CraftsDataFileName => EffectiveProfileMode == ProfileMode.Pve ? "craftsDataPVE.json" : "craftsData.json";
        public string CraftInstancesDataFileName => EffectiveProfileMode == ProfileMode.Pve ? "craftInstancesDataPVE.json" : "craftInstancesData.json";

        // Property to hold EffectiveProfileMode
        private ProfileMode effectiveProfileMode;
        public ProfileMode EffectiveProfileMode
        {
            get => effectiveProfileMode;
            set
            {
                if (effectiveProfileMode != value)
                {
                    effectiveProfileMode = value;
                    OnPropertyChanged(nameof(EffectiveProfileMode));
                    OnPropertyChanged(nameof(FavoritesFileName));
                    OnPropertyChanged(nameof(FavoritesItemOrderFileName));
                    OnPropertyChanged(nameof(CraftsDataFileName));
                    OnPropertyChanged(nameof(CraftInstancesDataFileName));
                    // Notify other dependent properties if any
                }
            }
        }

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

        // Tarkov Tracker API
        private bool isTarkovTrackerApiEnabled;
        public bool IsTarkovTrackerApiEnabled
        {
            get => isTarkovTrackerApiEnabled;
            set
            {
                if (isTarkovTrackerApiEnabled != value)
                {
                    if (!value)
                    {
                        _isManualCraftSource = true;
                        _isManualHideoutSource = true;
                        OnPropertyChanged(nameof(IsManualCraftSource));
                        OnPropertyChanged(nameof(IsManualHideoutSource));
                    }
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
        private bool hideMinimapWhenOutOfRaid;
        public bool HideMinimapWhenOutOfRaid
        {
            get => hideMinimapWhenOutOfRaid;
            set
            {
                if (hideMinimapWhenOutOfRaid != value)
                {
                    hideMinimapWhenOutOfRaid = value;
                    OnPropertyChanged(nameof(HideMinimapWhenOutOfRaid));
                }
            }
        }

        private bool showMinimapWhenMatching;
        public bool ShowMinimapWhenMatching
        {
            get => showMinimapWhenMatching;
            set
            {
                if (showMinimapWhenMatching != value)
                {
                    showMinimapWhenMatching = value;
                    OnPropertyChanged(nameof(ShowMinimapWhenMatching));
                }
            }
        }

        private bool showMinimapWhenInRaid;
        public bool ShowMinimapWhenInRaid
        {
            get => showMinimapWhenInRaid;
            set
            {
                if (showMinimapWhenInRaid != value)
                {
                    showMinimapWhenInRaid = value;
                    OnPropertyChanged(nameof(ShowMinimapWhenInRaid));
                }
            }
        }

        private bool hideCraftingUIWhenInRaid;
        public bool HideCraftingUIWhenInRaid
        {
            get => hideCraftingUIWhenInRaid;
            set
            {
                if (hideCraftingUIWhenInRaid != value)
                {
                    hideCraftingUIWhenInRaid = value;
                    OnPropertyChanged(nameof(HideCraftingUIWhenInRaid));
                }
            }
        }

        private bool hideOtherWindowButtonsWhenInRaid;
        public bool HideOtherWindowButtonsWhenInRaid
        {
            get => hideOtherWindowButtonsWhenInRaid;
            set
            {
                if (hideOtherWindowButtonsWhenInRaid != value)
                {
                    hideOtherWindowButtonsWhenInRaid = value;
                    OnPropertyChanged(nameof(HideOtherWindowButtonsWhenInRaid));
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

        private int currentCraftingLevelPVE;
        public int CurrentCraftingLevelPVE
        {
            get => currentCraftingLevelPVE;
            set
            {
                if (currentCraftingLevelPVE != value)
                {
                    currentCraftingLevelPVE = value;
                    OnPropertyChanged(nameof(CurrentCraftingLevelPVE));
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

        private bool hideRaidTimerOn10MinutesLeft;
        public bool HideRaidTimerOn10MinutesLeft
        {
            get => hideRaidTimerOn10MinutesLeft;
            set
            {
                if (hideRaidTimerOn10MinutesLeft != value)
                {
                    hideRaidTimerOn10MinutesLeft = value;
                    OnPropertyChanged(nameof(HideRaidTimerOn10MinutesLeft));
                }
            }
        }

        private bool hideRaidTimerOnRaidEnd;
        public bool HideRaidTimerOnRaidEnd
        {
            get => hideRaidTimerOnRaidEnd;
            set
            {
                if (hideRaidTimerOnRaidEnd != value)
                {
                    hideRaidTimerOnRaidEnd = value;
                    OnPropertyChanged(nameof(HideRaidTimerOnRaidEnd));
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

        private bool autoCompleteSubTasksForFoundItems;
        public bool AutoCompleteSubTasksForFoundItems
        {
            get => autoCompleteSubTasksForFoundItems;
            set
            {
                if (autoCompleteSubTasksForFoundItems != value)
                {
                    autoCompleteSubTasksForFoundItems = value;
                    OnPropertyChanged(nameof(AutoCompleteSubTasksForFoundItems));
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

        // Add the following property for PVE Hideout Module Settings
        private ObservableCollection<HideoutModuleSetting> _hideoutModuleSettingsPVE;
        public ObservableCollection<HideoutModuleSetting> HideoutModuleSettingsPVE
        {
            get => _hideoutModuleSettingsPVE;
            set
            {
                if (_hideoutModuleSettingsPVE != value)
                {
                    _hideoutModuleSettingsPVE = value;
                    OnPropertyChanged(nameof(HideoutModuleSettingsPVE));
                }
            }
        }

        // Add the following property for TT Hideout Module Settings
        private ObservableCollection<HideoutModuleSetting> _hideoutModuleSettingsTT;
        public ObservableCollection<HideoutModuleSetting> HideoutModuleSettingsTT
        {
            get => _hideoutModuleSettingsTT;
            set
            {
                if (_hideoutModuleSettingsTT != value)
                {
                    _hideoutModuleSettingsTT = value;
                    OnPropertyChanged(nameof(HideoutModuleSettingsTT));
                }
            }
        }

        // Add the following property for TT PVE Hideout Module Settings
        private ObservableCollection<HideoutModuleSetting> _hideoutModuleSettingsPVETT;
        public ObservableCollection<HideoutModuleSetting> HideoutModuleSettingsPVETT
        {
            get => _hideoutModuleSettingsPVETT;
            set
            {
                if (_hideoutModuleSettingsPVETT != value)
                {
                    _hideoutModuleSettingsPVETT = value;
                    OnPropertyChanged(nameof(HideoutModuleSettingsPVETT));
                }
            }
        }

        // Add the following property for Effective Hideout Module Settings
        private ObservableCollection<HideoutModuleSetting> _effectiveHideoutModuleSettings;
        public ObservableCollection<HideoutModuleSetting> EffectiveHideoutModuleSettings
        {
            get => _effectiveHideoutModuleSettings;
            set
            {
                if (_effectiveHideoutModuleSettings != value)
                {
                    _effectiveHideoutModuleSettings = value;
                    OnPropertyChanged(nameof(EffectiveHideoutModuleSettings));
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

        // Collection for PVE Craft Module Settings
        private ObservableCollection<CraftModuleSetting> _craftModuleSettingsPVE;
        public ObservableCollection<CraftModuleSetting> CraftModuleSettingsPVE
        {
            get => _craftModuleSettingsPVE;
            set
            {
                if (_craftModuleSettingsPVE != value)
                {
                    _craftModuleSettingsPVE = value;
                    OnPropertyChanged(nameof(CraftModuleSettingsPVE));
                }
            }
        }

        // Collection for TT Craft Module Settings
        private ObservableCollection<CraftModuleSetting> _craftModuleSettingsTT;
        public ObservableCollection<CraftModuleSetting> CraftModuleSettingsTT
        {
            get => _craftModuleSettingsTT;
            set
            {
                if (_craftModuleSettingsTT != value)
                {
                    _craftModuleSettingsTT = value;
                    OnPropertyChanged(nameof(CraftModuleSettingsTT));
                }
            }
        }

        // Collection for TT PVE Craft Module Settings
        private ObservableCollection<CraftModuleSetting> _craftModuleSettingsPVETT;
        public ObservableCollection<CraftModuleSetting> CraftModuleSettingsPVETT
        {
            get => _craftModuleSettingsPVETT;
            set
            {
                if (_craftModuleSettingsPVETT != value)
                {
                    _craftModuleSettingsPVETT = value;
                    OnPropertyChanged(nameof(CraftModuleSettingsPVETT));
                }
            }
        }

        // Collection for Effective Craft Module Settings
        private ObservableCollection<CraftModuleSetting> _effectiveCraftModuleSettings;
        public ObservableCollection<CraftModuleSetting> EffectiveCraftModuleSettings
        {
            get => _effectiveCraftModuleSettings;
            set
            {
                if (_effectiveCraftModuleSettings != value)
                {
                    _effectiveCraftModuleSettings = value;
                    OnPropertyChanged(nameof(EffectiveCraftModuleSettings));
                }
            }
        }

        // Add the following property for Keybinds
        private ObservableCollection<KeybindEntry> _keybinds;
        public ObservableCollection<KeybindEntry> Keybinds
        {
            get => _keybinds;
            set
            {
                if (_keybinds != value)
                {
                    _keybinds = value;
                    OnPropertyChanged(nameof(Keybinds));
                }
            }
        }

        public AppConfig()
        {
            // Initialize collections to prevent null references
            _craftModuleSettings = new ObservableCollection<CraftModuleSetting>();
            _hideoutModuleSettings = new ObservableCollection<HideoutModuleSetting>();
            _craftModuleSettingsPVE = new ObservableCollection<CraftModuleSetting>();
            _hideoutModuleSettingsPVE = new ObservableCollection<HideoutModuleSetting>();
            _hideoutModuleSettingsTT = new ObservableCollection<HideoutModuleSetting>();
            _hideoutModuleSettingsPVETT = new ObservableCollection<HideoutModuleSetting>();
            _craftModuleSettingsTT = new ObservableCollection<CraftModuleSetting>();
            _craftModuleSettingsPVETT = new ObservableCollection<CraftModuleSetting>();
            _effectiveCraftModuleSettings = new ObservableCollection<CraftModuleSetting>();
            _effectiveHideoutModuleSettings = new ObservableCollection<HideoutModuleSetting>();
            _keybinds = new ObservableCollection<KeybindEntry>();
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class HideoutModuleSetting : INotifyPropertyChanged
    {
        public string Id { get; set; }
        public string ModuleName { get; set; }
        public string StationImageLink { get; set; }
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

        private BitmapImage _stationIcon;
        public BitmapImage StationIcon
        {
            get
            {
                if (ModuleName.Equals("Cultist Circle", StringComparison.OrdinalIgnoreCase) || ModuleName.Equals("Gear Rack", StringComparison.OrdinalIgnoreCase))
                {
                    // Use local icon
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string iconFileName = ModuleName.Equals("Cultist Circle", StringComparison.OrdinalIgnoreCase)
                        ? "Cultist Circle.png"
                        : "Gear Rack.png";
                    string localIconPath = Path.Combine(baseDir, "StationIcons", iconFileName);
                    if (File.Exists(localIconPath))
                    {
                        _stationIcon = new BitmapImage(new Uri(localIconPath, UriKind.Absolute));
                    }
                    else
                    {
                        // If icon doesn't exist locally, consider loading a default fallback
                        string defaultIconPath = Path.Combine(baseDir, "StationIcons", "default.png");
                        if (File.Exists(defaultIconPath))
                        {
                            _stationIcon = new BitmapImage(new Uri(defaultIconPath, UriKind.Absolute));
                        }
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(StationImageLink))
                    {
                        _stationIcon = new BitmapImage(new Uri(StationImageLink, UriKind.Absolute));
                    }
                    else
                    {
                        // If StationImageLink is empty, fallback to a default icon
                        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        string defaultIconPath = Path.Combine(baseDir, "StationIcons", "default.png");
                        if (File.Exists(defaultIconPath))
                        {
                            _stationIcon = new BitmapImage(new Uri(defaultIconPath, UriKind.Absolute));
                        }
                    }
                }
                return _stationIcon;
            }
        }

        public HideoutModuleSetting()
        {
            AvailableLevels = new ObservableCollection<int>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name) =>
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
        public string CraftId { get; set; } // Unique identifier for the craft
        public string CraftName { get; set; } // Name of the craft
        public string CraftIconLink { get; set; } // URL to the craft's icon
        public string TraderIconLink { get; set; } // URL to the trader's icon
        public string QuestName { get; set; } // Name of the quest that unlocks the craft
        public string QuestId { get; set; } // Unique identifier for the quest that unlocks the craft

        private bool _isUnlocked;
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
        public void OnPropertyChanged(string propertyName) =>
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

    public static class TarkovApiService
    {
        private const string ApiUrl = "https://api.tarkov.dev/graphql";

        // Cached data and flags
        private static bool isCraftableItemsDataLoaded = false;
        private static GraphQLCraftsResponse craftableItemsData;

        private static bool isCraftModuleSettingsDataLoaded = false;
        private static GraphQLCraftsResponse craftModuleSettingsData;

        private static bool isRequiredItemsDataLoaded = false;
        private static GraphQLRequiredItemsResponse requiredItemsResponseData;

        private static Task<GraphQLCraftsResponse> craftableItemsTask;
        private static Task<GraphQLCraftsResponse> craftModuleSettingsTask;
        private static Task<GraphQLRequiredItemsResponse> requiredItemsTask;

        public static async Task<GraphQLCraftsResponse> GetCraftableItemsDataAsync()
        {
            if (craftableItemsTask != null) // If a fetch is already in progress, return the ongoing Task.
            {
                return await craftableItemsTask;
            }

            if (isCraftableItemsDataLoaded)
            {
                return craftableItemsData;
            }

            craftableItemsTask = FetchCraftableItemsDataAsync();
            var result = await craftableItemsTask;

            craftableItemsTask = null; // Reset the Task when completed.
            return result;
        }

        private static async Task<GraphQLCraftsResponse> FetchCraftableItemsDataAsync()
        {
            var queryObject = new
            {
                query = @"{
                    crafts {
                        id
                        level
                        station {
                            id
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

            try
            {
                var responseContent = await PostQueryAsync(queryObject);
                var graphQLResponse = JsonConvert.DeserializeObject<GraphQLCraftsResponse>(responseContent);

                if (graphQLResponse != null && graphQLResponse.Data != null && graphQLResponse.Data.Crafts != null)
                {
                    craftableItemsData = graphQLResponse;
                    isCraftableItemsDataLoaded = true;
                    return craftableItemsData;
                }
                else if (graphQLResponse != null && graphQLResponse.Errors != null && graphQLResponse.Errors.Length > 0)
                {
                    var errorMessages = string.Join("\n", graphQLResponse.Errors.Select(e => e.Message));
                    MessageBox.Show($"GraphQL errors:\n{errorMessages}");
                }
                else
                {
                    MessageBox.Show("No data received from GraphQL API (Craftable Items).");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fetching craftable items: {ex.Message}");
            }

            return null;
        }

        public static async Task<GraphQLCraftsResponse> GetCraftModuleSettingsDataAsync()
        {
            if (craftModuleSettingsTask != null) // If a fetch is already in progress, return the ongoing Task.
            {
                return await craftModuleSettingsTask;
            }

            if (isCraftModuleSettingsDataLoaded)
            {
                return craftModuleSettingsData;
            }

            craftModuleSettingsTask = FetchCraftModuleSettingsDataAsync();
            var result = await craftModuleSettingsTask;

            craftModuleSettingsTask = null; // Reset the Task when completed.
            return result;
        }

        private static async Task<GraphQLCraftsResponse> FetchCraftModuleSettingsDataAsync()
        {
            var queryObject = new
            {
                query = @"{
                    tasks {
                        id
                        name
                        startRewards {
                            craftUnlock {
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
                        }
                        finishRewards {
                            craftUnlock {
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
                        }
                        trader {
                            id
                            name
                            imageLink
                        }
                   }
                }"
            };

            try
            {
                var responseContent = await PostQueryAsync(queryObject);
                var graphQLResponse = JsonConvert.DeserializeObject<GraphQLCraftsResponse>(responseContent);

                if (graphQLResponse != null && graphQLResponse.Data != null && graphQLResponse.Data.Tasks != null)
                {
                    craftModuleSettingsData = graphQLResponse;
                    isCraftModuleSettingsDataLoaded = true;
                    return craftModuleSettingsData;
                }
                else if (graphQLResponse != null && graphQLResponse.Errors != null && graphQLResponse.Errors.Length > 0)
                {
                    var errorMessages = string.Join("\n", graphQLResponse.Errors.Select(e => e.Message));
                    MessageBox.Show($"GraphQL errors:\n{errorMessages}");
                }
                else
                {
                    MessageBox.Show("No data received from GraphQL API (Craft Module Settings).");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fetching craft module settings: {ex.Message}");
            }

            return null;
        }

        public static async Task<GraphQLRequiredItemsResponse> GetRequiredItemsDataAsync()
        {
            if (requiredItemsTask != null) // If a fetch is already in progress, return the ongoing Task.
            {
                return await requiredItemsTask;
            }

            if (isRequiredItemsDataLoaded)
            {
                return requiredItemsResponseData;
            }

            requiredItemsTask = FetchRequiredItemsDataAsync();
            var result = await requiredItemsTask;

            requiredItemsTask = null; // Reset the Task when completed.
            return result;
        }

        private static async Task<GraphQLRequiredItemsResponse> FetchRequiredItemsDataAsync()
        {
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

            var queryObject = new { query = query };

            try
            {
                var responseContent = await PostQueryAsync(queryObject);
                if (responseContent == null) return null;

                var graphQLResponse = JsonConvert.DeserializeObject<GraphQLRequiredItemsResponse>(responseContent);

                if (graphQLResponse == null || graphQLResponse.Data == null)
                {
                    MessageBox.Show("No data received from GraphQL API (Required Items).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                // If we reach here, we have valid data
                requiredItemsResponseData = graphQLResponse;
                isRequiredItemsDataLoaded = true;
                return requiredItemsResponseData;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading required items data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return null;
        }

        private static async Task<string> PostQueryAsync(object queryObject)
        {
            using (HttpClient client = new HttpClient())
            {
                var queryJson = JsonConvert.SerializeObject(queryObject);
                var content = new StringContent(queryJson, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(ApiUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"API request failed with status code {response.StatusCode}: {response.ReasonPhrase}\nContent: {errorContent}");
                    return null;
                }

                return await response.Content.ReadAsStringAsync();
            }
        }
    }

    public class GraphQLRequiredItemsResponse
    {
        [JsonProperty("data")]
        public RequiredItemsData Data { get; set; }
    }

    public class RequiredItemsData
    {
        [JsonProperty("tasks")]
        public List<TaskInfo> Tasks { get; set; }

        [JsonProperty("hideoutStations")]
        public List<StationInfo> HideoutStations { get; set; }
    }

    public class TaskInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public TraderInfo Trader { get; set; }
        public List<TaskObjectiveInfo> Objectives { get; set; }
    }

    public class TraderInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ImageLink { get; set; }
    }

    public class TaskObjectiveInfo
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public List<ItemInfo> Items { get; set; }
        public int Count { get; set; }
        public bool FoundInRaid { get; set; }
    }

    public class ItemInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string IconLink { get; set; }
    }

    public class StationInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string NormalizedName { get; set; }
        public string ImageLink { get; set; }
        public List<StationLevelInfo> Levels { get; set; }
    }

    public class StationLevelInfo
    {
        public int Level { get; set; }
        public List<ItemRequirementInfo> ItemRequirements { get; set; }
    }

    public class ItemRequirementInfo
    {
        public ItemInfo Item { get; set; }
        public int Count { get; set; }
    }

    public static class CraftingDataManager
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static bool SaveCraftsDataWithPVE = false;
        private static bool SaveCraftInstancesDataWithPVE = false;

        // Create serializer settings with appropriate converters
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            Formatting = Formatting.Indented,
            DateTimeZoneHandling = DateTimeZoneHandling.Local,
            PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            Converters = new List<JsonConverter>
                {
                    new EFT_OverlayAPP.TimeSpanConverter(),
                    new IsoDateTimeConverter { DateTimeFormat = "o" }
                }
        };


        // Method to save crafts data
        public static void SaveCraftsData(List<CraftableItem> crafts)
        {
            if (SaveCraftsDataWithPVE)
            {
                try
                {
                    string json = JsonConvert.SerializeObject(crafts, SerializerSettings);
                    File.WriteAllText("craftsDataPVE.json", json);
                }
                catch (Exception ex)
                {
                    // Log or handle exceptions
                }
            }
            else
            {
                try
                {
                    string json = JsonConvert.SerializeObject(crafts, SerializerSettings);
                    File.WriteAllText("craftsData.json", json);
                }
                catch (Exception ex)
                {
                    // Log or handle exceptions
                }
            }
        }

        // Method to load crafts data
        public static List<CraftableItem> LoadCraftsData()
        {
            if (App.IsPVEMode)
            {
                SaveCraftsDataWithPVE = true;
                try
                {
                    if (File.Exists("craftsDataPVE.json"))
                    {
                        string json = File.ReadAllText("craftsDataPVE.json");
                        var crafts = JsonConvert.DeserializeObject<List<CraftableItem>>(json, SerializerSettings);
                        logger.Info($"Loaded {crafts.Count} crafts from craftsDataPVE.json.");
                        return crafts;
                    }
                    else
                    {
                        logger.Info($"No craftsDataPVE.json file found. Starting with no saved crafts.");
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error loading crafts data.");
                }
            }
            else
            {
                SaveCraftsDataWithPVE = false;
                try
                {
                    if (File.Exists("craftsData.json"))
                    {
                        string json = File.ReadAllText("craftsData.json");
                        var crafts = JsonConvert.DeserializeObject<List<CraftableItem>>(json, SerializerSettings);
                        logger.Info($"Loaded {crafts.Count} crafts from craftsData.json.");
                        return crafts;
                    }
                    else
                    {
                        logger.Info($"No craftsData.json file found. Starting with no saved crafts.");
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error loading crafts data.");
                }
            }

            return new List<CraftableItem>();
        }

        // Method to save craft instances data
        public static void SaveCraftInstancesData(List<CraftInstance> craftInstances)
        {
            if (SaveCraftInstancesDataWithPVE)
            {
                try
                {
                    string json = JsonConvert.SerializeObject(craftInstances, SerializerSettings);
                    File.WriteAllText("craftInstancesDataPVE.json", json);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error saving craft instances data.");
                }
            }
            else
            {
                try
                {
                    string json = JsonConvert.SerializeObject(craftInstances, SerializerSettings);
                    File.WriteAllText("craftInstancesData.json", json);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error saving craft instances data.");
                }
            }
        }

        // Method to load craft instances data
        public static List<CraftInstance> LoadCraftInstancesData()
        {
            if (App.IsPVEMode)
            {
                SaveCraftInstancesDataWithPVE = true;
                try
                {
                    if (File.Exists("craftInstancesDataPVE.json"))
                    {
                        string json = File.ReadAllText("craftInstancesDataPVE.json");
                        var craftInstances = JsonConvert.DeserializeObject<List<CraftInstance>>(json, SerializerSettings);
                        logger.Info($"Loaded {craftInstances.Count} craft instances from craftInstancesDataPVE.json.");
                        return craftInstances;
                    }
                    else
                    {
                        logger.Info($"No craftInstancesDataPVE.json file found. Starting with empty craft instances.");
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error loading craft instances data.");
                }
            }
            else
            {
                SaveCraftInstancesDataWithPVE = false;
                try
                {
                    if (File.Exists("craftInstancesData.json"))
                    {
                        string json = File.ReadAllText("craftInstancesData.json");
                        var craftInstances = JsonConvert.DeserializeObject<List<CraftInstance>>(json, SerializerSettings);
                        logger.Info($"Loaded {craftInstances.Count} craft instances from craftInstancesData.json.");
                        return craftInstances;
                    }
                    else
                    {
                        logger.Info($"No craftInstancesData.json file found. Starting with empty craft instances.");
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error loading craft instances data.");
                }
            }
            return new List<CraftInstance>();
        }
    }

    // Models for API responses
    public class TokenResponse
    {
        public List<string> Permissions { get; set; }
        public string Token { get; set; }
    }

    public class ProgressResponse
    {
        public ProgressData Data { get; set; }
        public ProgressMeta Meta { get; set; }
    }

    public class ProgressData
    {
        public List<TaskProgress> TasksProgress { get; set; }
        public List<HideoutModuleProgress> HideoutModulesProgress { get; set; }
        public string DisplayName { get; set; }
        public string UserId { get; set; }
        public int PlayerLevel { get; set; }
        public int GameEdition { get; set; }
        public string PmcFaction { get; set; }
    }

    public class TaskProgress
    {
        public string Id { get; set; }
        public bool Complete { get; set; }
        public bool Invalid { get; set; }
        public bool Failed { get; set; }
    }

    public class HideoutModuleProgress
    {
        public string Id { get; set; }
        public bool Complete { get; set; }
    }

    public class ProgressMeta
    {
        public string Self { get; set; }
    }

    public class TaskStatusBody
    {
        public string Id { get; set; }
        public string State { get; set; }

        public static TaskStatusBody From(TaskStatus status)
        {
            return new TaskStatusBody
            {
                State = status.ToString().ToLower() // e.g., "finished" -> "finished"
            };
        }
    }

    public enum TaskStatus
    {
        Finished,
        Failed,
        None
    }

    public interface ITarkovTrackerAPI
    {
        // Test the token
        [Get("/token")]
        [Headers("Authorization: Bearer")]
        Task<TokenResponse> TestToken([Header("Authorization")] string bearerToken);

        // Get progress
        [Get("/progress")]
        [Headers("Authorization: Bearer")]
        Task<ProgressResponse> GetProgress([Header("Authorization")] string bearerToken);

        // Update a single task status
        [Post("/progress/task/{id}")]
        [Headers("Authorization: Bearer")]
        Task<string> SetTaskStatus(string id, [Body] TaskStatusBody body, [Header("Authorization")] string bearerToken);

        // Update multiple task statuses
        [Post("/progress/tasks")]
        [Headers("Authorization: Bearer")]
        Task<string> SetTaskStatuses([Body] List<TaskStatusBody> body, [Header("Authorization")] string bearerToken);
    }

    public class TarkovTrackerService
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private const string BaseUrl = "https://tarkovtracker.io/api/v2";

        private ITarkovTrackerAPI apiClient;
        private string currentToken;
        private AppConfig appConfig;

        // Events to notify other parts of the application
        public event EventHandler TokenValidated;
        public event EventHandler TokenInvalid;
        public event EventHandler ProgressRetrieved;

        public TarkovTrackerService(AppConfig config)
        {
            appConfig = config;
            InitializeClient();
        }

        private void InitializeClient()
        {
            apiClient = RestService.For<ITarkovTrackerAPI>(BaseUrl);
            UpdateToken();
        }

        // Call this method whenever the profile mode or API key changes
        public void UpdateToken()
        {
            if (App.IsPVEMode)
            {
                currentToken = appConfig.PveApiKey;
            }
            else
            {
                currentToken = appConfig.PvpApiKey;
            }

            logger.Info($"Using API Token: {currentToken}");
        }

        // Validate the current token
        public async Task<bool> ValidateTokenAsync()
        {
            if (string.IsNullOrWhiteSpace(currentToken))
            {
                logger.Warn("API token is empty.");
                TokenInvalid?.Invoke(this, EventArgs.Empty);
                return false;
            }

            try
            {
                var response = await apiClient.TestToken($"Bearer {currentToken}");
                if (response.Permissions.Contains("WP")) // Assuming "WP" is a valid permission
                {
                    TokenValidated?.Invoke(this, EventArgs.Empty);
                    logger.Info("API token validated successfully.");
                    return true;
                }
                else
                {
                    TokenInvalid?.Invoke(this, EventArgs.Empty);
                    logger.Warn("API token does not have required permissions.");
                    return false;
                }
            }
            catch (ApiException ex)
            {
                if (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    TokenInvalid?.Invoke(this, EventArgs.Empty);
                    logger.Error("API token is unauthorized.");
                }
                else if (ex.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    logger.Error("Rate limited by Tarkov Tracker API.");
                }
                else
                {
                    logger.Error(ex, $"API exception occurred: {ex.Message}");
                }
                return false;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Unexpected error during token validation: {ex.Message}");
                return false;
            }
        }

        // Get progress data
        public async Task<ProgressResponse> GetProgressAsync()
        {
            UpdateToken();

            if (string.IsNullOrWhiteSpace(currentToken))
            {
                logger.Warn("API token is empty. Cannot retrieve progress.");
                return null;
            }

            try
            {
                var response = await apiClient.GetProgress($"Bearer {currentToken}");
                ProgressRetrieved?.Invoke(this, EventArgs.Empty);
                logger.Info("Progress data retrieved successfully.");
                return response;
            }
            catch (ApiException ex)
            {
                if (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    TokenInvalid?.Invoke(this, EventArgs.Empty);
                    logger.Error("API token is unauthorized.");
                }
                else if (ex.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    logger.Error("Rate limited by Tarkov Tracker API.");
                }
                else
                {
                    logger.Error(ex, $"API exception occurred: {ex.Message}");
                }
                return null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Unexpected error during GetProgressAsync: {ex.Message}");
                return null;
            }
        }

        // Update a single task status
        public async Task<bool> UpdateTaskStatusAsync(string taskId, TaskStatus status)
        {
            if (string.IsNullOrWhiteSpace(currentToken))
            {
                logger.Warn("API token is empty. Cannot update task status.");
                return false;
            }

            try
            {
                var body = TaskStatusBody.From(status);
                var response = await apiClient.SetTaskStatus(taskId, body, $"Bearer {currentToken}");
                logger.Info($"Task {taskId} status updated to {status}.");
                return true;
            }
            catch (ApiException ex)
            {
                if (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    TokenInvalid?.Invoke(this, EventArgs.Empty);
                    logger.Error("API token is unauthorized.");
                }
                else if (ex.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    logger.Error("Rate limited by Tarkov Tracker API.");
                }
                else
                {
                    logger.Error(ex, $"API exception occurred: {ex.Message}");
                }
                return false;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Unexpected error during UpdateTaskStatusAsync: {ex.Message}");
                return false;
            }
        }

        // Update multiple task statuses
        public async Task<bool> UpdateMultipleTaskStatusesAsync(Dictionary<string, TaskStatus> taskStatuses)
        {
            if (string.IsNullOrWhiteSpace(currentToken))
            {
                logger.Warn("API token is empty. Cannot update multiple task statuses.");
                return false;
            }

            try
            {
                var body = new List<TaskStatusBody>();
                foreach (var kvp in taskStatuses)
                {
                    var taskStatusBody = TaskStatusBody.From(kvp.Value);
                    taskStatusBody.Id = kvp.Key;
                    body.Add(taskStatusBody);
                }

                var response = await apiClient.SetTaskStatuses(body, $"Bearer {currentToken}");
                logger.Info($"Multiple task statuses updated successfully.");
                return true;
            }
            catch (ApiException ex)
            {
                if (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    TokenInvalid?.Invoke(this, EventArgs.Empty);
                    logger.Error("API token is unauthorized.");
                }
                else if (ex.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    logger.Error("Rate limited by Tarkov Tracker API.");
                }
                else
                {
                    logger.Error(ex, $"API exception occurred: {ex.Message}");
                }
                return false;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Unexpected error during UpdateMultipleTaskStatusesAsync: {ex.Message}");
                return false;
            }
        }

        // Fetch hideout station levels
        public async Task<List<HideoutModuleProgress>> GetHideoutModuleLevelsAsync()
        {
            var progress = await GetProgressAsync();
            if (progress?.Data?.HideoutModulesProgress != null)
            {
                logger.Info("Hideout module levels fetched successfully.");
                return progress.Data.HideoutModulesProgress;
            }
            else
            {
                logger.Warn("Hideout module levels data is null.");
                return new List<HideoutModuleProgress>();
            }
        }

        // Fetch finished quests
        public async Task<List<TaskProgress>> GetFinishedQuestsAsync()
        {
            var progress = await GetProgressAsync();
            if (progress?.Data?.TasksProgress != null)
            {
                var finishedTasks = progress.Data.TasksProgress.FindAll(t => t.Complete);
                logger.Info($"Fetched {finishedTasks.Count} finished quests.");
                return finishedTasks;
            }
            else
            {
                logger.Warn("Tasks progress data is null.");
                return new List<TaskProgress>();
            }
        }

        // Additional methods can be added here for other API interactions
    }

    // Define a class to represent the processed structure
    public class ProcessedLevel
    {
        public string Id { get; set; }
        public int Level { get; set; }
        public bool Complete { get; set; }
    }

    public class OthersWindowDataBinding : INotifyPropertyChanged
    {
        public GameState GameState { get; set; }
        public AppConfig Config { get; set; }
        public MainWindow Main { get; set; }

        private bool isInRaid;
        public bool IsInRaid
        {
            get => isInRaid;
            set
            {
                if (isInRaid != value)
                {
                    isInRaid = value;
                    OnPropertyChanged(nameof(IsInRaid));
                }
            }
        }

        private bool hideOtherWindowButtonsWhenInRaid;
        public bool HideOtherWindowButtonsWhenInRaid
        {
            get => hideOtherWindowButtonsWhenInRaid;
            set
            {
                if (hideOtherWindowButtonsWhenInRaid != value)
                {
                    hideOtherWindowButtonsWhenInRaid = value;
                    OnPropertyChanged(nameof(HideOtherWindowButtonsWhenInRaid));
                }
            }
        }

        private bool manualOtherWindowButtonsVisibilityOverride = false;
        public bool ManualOtherWindowButtonsVisibilityOverride
        {
            get => manualOtherWindowButtonsVisibilityOverride;
            set
            {
                if (manualOtherWindowButtonsVisibilityOverride != value)
                {
                    manualOtherWindowButtonsVisibilityOverride = value;
                    OnPropertyChanged(nameof(ManualOtherWindowButtonsVisibilityOverride));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
