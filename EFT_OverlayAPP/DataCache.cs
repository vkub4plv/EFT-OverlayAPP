using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace EFT_OverlayAPP
{
    public static class DataCache
    {
        // Cached data collections
        public static List<CraftableItem> CraftableItems { get; private set; } = new List<CraftableItem>();

        // Indicates whether data has been loaded
        public static bool IsDataLoaded { get; set; } = false;

        // Event to notify when data loading is complete
        public static event Action DataLoaded;

        // List to store favorite item IDs
        private static List<string> favoriteIds = new List<string>();

        // List to store order of categories in favorite
        public static List<string> CategoryOrder { get; private set; } = new List<string>();

        // Fetch data from the GraphQL API
        public static async Task<List<CraftableItem>> FetchCraftableItemsAsync()
        {
            var craftableItems = new List<CraftableItem>();
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    var queryObject = new
                    {
                        query = @"{
                            crafts {
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
                        }"
                    };
                    var queryJson = JsonConvert.SerializeObject(queryObject);
                    var content = new StringContent(queryJson, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync("https://api.tarkov.dev/graphql", content);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();

                        // Deserialize the response
                        var graphQLResponse = JsonConvert.DeserializeObject<GraphQLCraftsResponse>(responseContent);

                        if (graphQLResponse.Data != null && graphQLResponse.Data.Crafts != null)
                        {
                            int index = 0;
                            foreach (var craft in graphQLResponse.Data.Crafts)
                            {
                                var craftableItem = new CraftableItem
                                {
                                    Id = craft.Id,
                                    Station = craft.Station?.Name ?? "Unknown",
                                    CraftTime = TimeSpan.FromSeconds(craft.Duration ?? 0),
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
                            // Store the order of categories
                            CategoryOrder = craftableItems.Select(item => item.Station)
                                                          .Distinct()
                                                          .ToList();

                            // Set StationIndex for each item
                            foreach (var item in craftableItems)
                            {
                                item.StationIndex = CategoryOrder.IndexOf(item.Station);
                            }
                        }
                        else if (graphQLResponse.Errors != null && graphQLResponse.Errors.Length > 0)
                        {
                            var errorMessages = string.Join("\n", graphQLResponse.Errors.Select(e => e.Message));
                            MessageBox.Show($"GraphQL errors:\n{errorMessages}");
                        }
                        else
                        {
                            MessageBox.Show("No data received from GraphQL API.");
                        }
                    }
                    else
                    {
                        // Log the status code and reason
                        string errorContent = await response.Content.ReadAsStringAsync();
                        MessageBox.Show($"API request failed with status code {response.StatusCode}: {response.ReasonPhrase}\nContent: {errorContent}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error fetching craftable items: {ex.Message}");
                }
            }
            return craftableItems;
        }

        public static void AddFavoriteId(string id)
        {
            if (!favoriteIds.Contains(id))
            {
                favoriteIds.Add(id);
                SaveFavorites();
            }
        }

        public static void RemoveFavoriteId(string id)
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
            File.WriteAllText("favorites.json", json);
        }

        private static void LoadFavorites()
        {
            if (File.Exists("favorites.json"))
            {
                string json = File.ReadAllText("favorites.json");
                favoriteIds = JsonConvert.DeserializeObject<List<string>>(json);
            }
        }

        // Methods for saving and loading favorite item order
        public static void SaveFavoriteItemOrder(IList<CraftableItem> favoriteItems)
        {
            var itemOrder = favoriteItems.Select(i => i.Id).ToList();
            string json = JsonConvert.SerializeObject(itemOrder);
            File.WriteAllText("favoritesItemOrder.json", json);
        }

        public static void LoadFavoriteItemOrder(ObservableCollection<CraftableItem> favoriteItems)
        {
            if (File.Exists("favoritesItemOrder.json"))
            {
                string json = File.ReadAllText("favoritesItemOrder.json");
                var itemOrder = JsonConvert.DeserializeObject<List<string>>(json);

                var sortedItems = itemOrder
                    .Select(id => favoriteItems.FirstOrDefault(i => i.Id == id))
                    .Where(i => i != null)
                    .ToList();

                // Add any new items that were not in the saved order
                var newItems = favoriteItems.Where(i => !itemOrder.Contains(i.Id)).ToList();

                // Clear and re-populate the collection
                favoriteItems.Clear();
                foreach (var item in sortedItems.Concat(newItems))
                {
                    favoriteItems.Add(item);
                }
            }
        }

        public static async Task LoadDataAsync()
        {
            if (!IsDataLoaded)
            {
                LoadFavorites();

                CraftableItems = await FetchCraftableItemsAsync();

                foreach (var item in CraftableItems)
                {
                    item.IsFavorite = favoriteIds.Contains(item.Id);
                    // No need to subscribe to PropertyChanged here
                }

                IsDataLoaded = true;

                DataLoaded?.Invoke();
            }
        }

        public static void ClearData()
        {
            CraftableItems.Clear();
            IsDataLoaded = false;
        }
    }
}
