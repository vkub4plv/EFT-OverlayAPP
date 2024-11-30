using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace EFT_OverlayAPP
{
    public static class CraftingDataManager
    {
        private static readonly string CraftsDataFilePath = "craftsData.json";

        // Method to save crafts data
        public static void SaveCraftsData(List<CraftableItem> crafts)
        {
            try
            {
                string json = JsonConvert.SerializeObject(crafts, Formatting.Indented, new JsonSerializerSettings
                {
                    // Exclude properties that should not be serialized
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    TypeNameHandling = TypeNameHandling.Auto
                });
                File.WriteAllText(CraftsDataFilePath, json);
            }
            catch (Exception ex)
            {
                // Handle exceptions
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
                    var crafts = JsonConvert.DeserializeObject<List<CraftableItem>>(json, new JsonSerializerSettings
                    {
                        // Handle circular references if any
                        PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                        TypeNameHandling = TypeNameHandling.Auto
                    });
                    return crafts;
                }
            }
            catch (Exception ex)
            {
                // Log or handle exceptions
                // For example: MessageBox.Show($"Error loading crafts data: {ex.Message}");
            }
            return new List<CraftableItem>();
        }
    }
}
