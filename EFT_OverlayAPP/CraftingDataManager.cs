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
}