using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace EFT_OverlayAPP
{
    public static class CraftingDataManager
    {
        private static readonly string CraftsDataFilePath = "craftsData.json";

        // Create serializer settings with appropriate converters
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            Formatting = Formatting.Indented,
            Converters = new List<JsonConverter>
            {
                new EFT_OverlayAPP.TimeSpanConverter(),
                new IsoDateTimeConverter { DateTimeFormat = "o" } // Use ISO 8601 format
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
                    return crafts;
                }
            }
            catch (Exception ex)
            {
                // Log or handle exceptions
            }
            return new List<CraftableItem>();
        }
    }
}