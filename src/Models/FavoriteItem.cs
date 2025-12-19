using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SolutionFavorites.Models
{
    /// <summary>
    /// Represents a favorite file item.
    /// </summary>
    public class FavoriteItem
    {
        /// <summary>
        /// Unique identifier for this favorite item.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Display name shown in Solution Explorer.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Full path to the file.
        /// </summary>
        [JsonProperty("filePath")]
        public string FilePath { get; set; }

        /// <summary>
        /// Sort order within the favorites list.
        /// </summary>
        [JsonProperty("sortOrder")]
        public int SortOrder { get; set; }

        /// <summary>
        /// Creates a new file favorite.
        /// </summary>
        public static FavoriteItem CreateFile(string filePath, string name = null)
        {
            return new FavoriteItem
            {
                Name = name ?? System.IO.Path.GetFileName(filePath),
                FilePath = filePath
            };
        }
    }

    /// <summary>
    /// Root container for all favorites data.
    /// </summary>
    public class FavoritesData
    {
        [JsonProperty("version")]
        public int Version { get; set; } = 1;

        [JsonProperty("items")]
        public List<FavoriteItem> Items { get; set; } = new List<FavoriteItem>();
    }
}
