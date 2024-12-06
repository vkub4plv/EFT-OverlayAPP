using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace EFT_OverlayAPP
{
    public static class CraftingDataManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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
        public static void SaveCraftsData(List<CraftableItem> crafts, string filePath)
        {
            try
            {
                string json = JsonConvert.SerializeObject(crafts, SerializerSettings);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                // Log or handle exceptions
            }
        }

        // Method to load crafts data
        public static List<CraftableItem> LoadCraftsData(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var crafts = JsonConvert.DeserializeObject<List<CraftableItem>>(json, SerializerSettings);
                    Logger.Info($"Loaded {crafts.Count} crafts from {filePath}.");
                    return crafts;
                }
                else
                {
                    Logger.Info($"No {filePath} file found. Starting with no saved crafts.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading crafts data.");
            }
            return new List<CraftableItem>();
        }

        // Method to save craft instances data
        public static void SaveCraftInstancesData(List<CraftInstance> craftInstances, string filePath)
        {
            try
            {
                string json = JsonConvert.SerializeObject(craftInstances, SerializerSettings);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error saving craft instances data.");
            }
        }

        // Method to load craft instances data
        public static List<CraftInstance> LoadCraftInstancesData(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var craftInstances = JsonConvert.DeserializeObject<List<CraftInstance>>(json, SerializerSettings);
                    Logger.Info($"Loaded {craftInstances.Count} craft instances from {filePath}.");
                    return craftInstances;
                }
                else
                {
                    Logger.Info($"No {filePath} file found. Starting with empty craft instances.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading craft instances data.");
            }
            return new List<CraftInstance>();
        }

    }
}