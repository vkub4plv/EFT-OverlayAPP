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
                            CraftStartTime = DateTime.UtcNow;
                            CraftCompletedTime = null;
                            CraftStoppedTime = null;
                            CraftFinishedTime = null;
                        }
                        else if (oldStatus == CraftStatus.InProgress && craftStatus == CraftStatus.NotStarted)
                        {
                            // Stopping an active craft
                            CraftStoppedTime = DateTime.UtcNow;
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
                if (CraftStatus == CraftStatus.InProgress && CraftStartTime.HasValue)
                {
                    var now = DateTime.UtcNow;
                    var elapsed = DateTime.UtcNow - CraftStartTime.Value;
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

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TimeSpanConverter : JsonConverter<TimeSpan>
    {
        // Serialize TimeSpan to a string
        public override void WriteJson(JsonWriter writer, TimeSpan value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString("c")); // "c" format is "hh:mm:ss"
        }

        // Deserialize TimeSpan from a string
        public override TimeSpan ReadJson(JsonReader reader, Type objectType, TimeSpan existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string timeSpanString = (string)reader.Value;
            return TimeSpan.Parse(timeSpanString);
        }
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
        [JsonProperty("CraftableItemId")]
        public string CraftableItemId => CraftableItem?.Id;

        [JsonProperty("Station")]
        public string Station => CraftableItem?.Station;

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
}
