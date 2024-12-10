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
    class Converters
    {
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
                    int totalHours = (int)timeSpan.TotalHours;
                    return $"{totalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
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
        // Converts a boolean to its inverse
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return true;
        }

        // Converts back the inverse boolean to original
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return true;
        }
    }

    public class InverseBooleanVisibilityConverter : IValueConverter
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

    public class NullToVisibilityConverter : IValueConverter
    {
        // Converts a nullable object to Visibility
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        // Not implemented
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class IsEliteLevelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double sliderValue)
            {
                return sliderValue == 51 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ProfileModeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ProfileMode mode)
            {
                switch (mode)
                {
                    case ProfileMode.Automatic:
                        return "Automatic";
                    case ProfileMode.Regular:
                        return "Regular (PVP)";
                    case ProfileMode.Pve:
                        return "PVE";
                    default:
                        return "Automatic";
                }
            }
            return "Automatic";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string modeStr)
            {
                switch (modeStr)
                {
                    case "Automatic":
                        return ProfileMode.Automatic;
                    case "Regular (PVP)":
                        return ProfileMode.Regular;
                    case "PVE":
                        return ProfileMode.Pve;
                    default:
                        return ProfileMode.Automatic;
                }
            }
            return ProfileMode.Automatic;
        }
    }

    public class ProfileModeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string profileMode = value as string;
            if (string.Equals(profileMode, "PVE", StringComparison.OrdinalIgnoreCase))
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseProfileModeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string profileMode = value as string;
            if (!string.Equals(profileMode, "PVE", StringComparison.OrdinalIgnoreCase))
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
