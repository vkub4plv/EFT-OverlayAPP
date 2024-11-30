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

        private static readonly string CraftsDataFilePath = "craftsData.json";

        // Create serializer settings with appropriate converters
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            Formatting = Formatting.Indented,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
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
            try
            {
                string json = JsonConvert.SerializeObject(crafts, SerializerSettings);
                File.WriteAllText(CraftsDataFilePath, json);
            }
            catch (Exception ex)
            {
                // Log or handle exceptions
            }
        }

        // Method to load crafts data
        public static List<CraftableItem> LoadCraftsData()
        {
            try
            {
                if (File.Exists(CraftsDataFilePath))
                {
                    string json = File.ReadAllText(CraftsDataFilePath);
                    var crafts = JsonConvert.DeserializeObject<List<CraftableItem>>(json, SerializerSettings);
                    Logger.Info($"Loaded {crafts.Count} crafts from craftsData.json.");
                    return crafts;
                }
                else
                {
                    Logger.Info("No craftsData.json file found. Starting with no saved crafts.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading crafts data.");
            }
            return new List<CraftableItem>();
        }

        // File path for craft instances data
        private static readonly string CraftInstancesDataFilePath = "craftInstancesData.json";

        // Method to save craft instances data
        public static void SaveCraftInstancesData(List<CraftInstance> craftInstances)
        {
            try
            {
                string json = JsonConvert.SerializeObject(craftInstances, SerializerSettings);
                File.WriteAllText(CraftInstancesDataFilePath, json);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error saving craft instances data.");
            }
        }

        // Method to load craft instances data
        public static List<CraftInstance> LoadCraftInstancesData()
        {
            try
            {
                if (File.Exists(CraftInstancesDataFilePath))
                {
                    string json = File.ReadAllText(CraftInstancesDataFilePath);
                    var craftInstances = JsonConvert.DeserializeObject<List<CraftInstance>>(json, SerializerSettings);
                    Logger.Info($"Loaded {craftInstances.Count} craft instances from craftInstancesData.json.");
                    return craftInstances;
                }
                else
                {
                    Logger.Info("No craftInstancesData.json file found. Starting with empty craft instances.");
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