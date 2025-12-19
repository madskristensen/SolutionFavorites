using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;
using SolutionFavorites.Models;

namespace SolutionFavorites
{
    /// <summary>
    /// Manages persistence and operations for favorite files.
    /// </summary>
    internal sealed class FavoritesManager
    {
        private static FavoritesManager _instance;
        private static readonly object _lock = new object();

        private FavoritesData _data;
        private string _currentSolutionPath;
        private string _solutionDirectory;

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static FavoritesManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new FavoritesManager();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Event raised when favorites change.
        /// </summary>
        public event EventHandler FavoritesChanged;

        private FavoritesManager()
        {
            _data = new FavoritesData();
        }

        /// <summary>
        /// Ensures the solution path is loaded if a solution is open.
        /// </summary>
        private void EnsureSolutionPathLoaded()
        {
            if (!string.IsNullOrEmpty(_currentSolutionPath))
                return;

            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
                if (dte?.Solution?.FullName != null && !string.IsNullOrEmpty(dte.Solution.FullName))
                {
                    LoadForSolution(dte.Solution.FullName);
                }
            }
            catch
            {
                // Ignore if not on UI thread or DTE not available
            }
        }

        /// <summary>
        /// Gets the solution directory path.
        /// </summary>
        public string SolutionDirectory => _solutionDirectory;

        /// <summary>
        /// Converts an absolute file path to a solution-relative path.
        /// </summary>
        private string ToRelativePath(string absolutePath)
        {
            if (string.IsNullOrEmpty(_solutionDirectory) || string.IsNullOrEmpty(absolutePath))
                return absolutePath;

            try
            {
                var solutionUri = new Uri(_solutionDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
                var fileUri = new Uri(absolutePath);

                if (solutionUri.IsBaseOf(fileUri))
                {
                    var relativeUri = solutionUri.MakeRelativeUri(fileUri);
                    return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
                }
            }
            catch
            {
                // Fall back to absolute path if conversion fails
            }

            return absolutePath;
        }

        /// <summary>
        /// Converts a solution-relative path to an absolute file path.
        /// </summary>
        public string ToAbsolutePath(string relativePath)
        {
            if (string.IsNullOrEmpty(_solutionDirectory) || string.IsNullOrEmpty(relativePath))
                return relativePath;

            // If it's already an absolute path, return as-is
            if (Path.IsPathRooted(relativePath))
                return relativePath;

            try
            {
                return Path.GetFullPath(Path.Combine(_solutionDirectory, relativePath));
            }
            catch
            {
                return relativePath;
            }
        }

        /// <summary>
        /// Gets the favorites file path for the current solution.
        /// </summary>
        private string GetFavoritesFilePath(string solutionPath)
        {
            if (string.IsNullOrEmpty(solutionPath))
                return null;

            var solutionDir = Path.GetDirectoryName(solutionPath);
            var solutionName = Path.GetFileNameWithoutExtension(solutionPath);
            return Path.Combine(solutionDir, ".vs", solutionName, "favorites.json");
        }

        /// <summary>
        /// Loads favorites for the given solution.
        /// </summary>
        public void LoadForSolution(string solutionPath)
        {
            _currentSolutionPath = solutionPath;
            _solutionDirectory = Path.GetDirectoryName(solutionPath);
            _data = new FavoritesData();

            var filePath = GetFavoritesFilePath(solutionPath);
            if (filePath != null && File.Exists(filePath))
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    _data = JsonConvert.DeserializeObject<FavoritesData>(json) ?? new FavoritesData();
                }
                catch (Exception)
                {
                    _data = new FavoritesData();
                }
            }

            RaiseFavoritesChanged();
        }

        /// <summary>
        /// Saves the current favorites to disk.
        /// </summary>
        public void Save()
        {
            EnsureSolutionPathLoaded();

            var filePath = GetFavoritesFilePath(_currentSolutionPath);
            if (filePath == null)
                return;

            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(_data, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception)
            {
                // Silently fail - we don't want to interrupt the user
            }
        }

        /// <summary>
        /// Clears all favorites (used when solution closes).
        /// </summary>
        public void Clear()
        {
            _currentSolutionPath = null;
            _solutionDirectory = null;
            _data = new FavoritesData();
            RaiseFavoritesChanged();
        }

        /// <summary>
        /// Gets all root-level favorites.
        /// </summary>
        public IReadOnlyList<FavoriteItem> GetRootItems()
        {
            EnsureSolutionPathLoaded();

            return _data.Items
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Adds a file to favorites.
        /// </summary>
        public FavoriteItem AddFile(string filePath)
        {
            EnsureSolutionPathLoaded();

            // Convert to relative path for storage
            var relativePath = ToRelativePath(filePath);

            // Check if file already exists (compare both relative and absolute)
            if (_data.Items.Any(i => 
                i.FilePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase) ||
                ToAbsolutePath(i.FilePath).Equals(filePath, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            var item = FavoriteItem.CreateFile(relativePath);
            item.SortOrder = _data.Items.Count;
            _data.Items.Add(item);
            Save();
            RaiseFavoritesChanged();
            return item;
        }

        /// <summary>
        /// Removes a favorite item.
        /// </summary>
        public void Remove(string itemId)
        {
            var item = _data.Items.FirstOrDefault(i => i.Id == itemId);
            if (item == null)
                return;

            _data.Items.Remove(item);
            Save();
            RaiseFavoritesChanged();
        }

        /// <summary>
        /// Gets an item by ID.
        /// </summary>
        public FavoriteItem GetItem(string itemId)
        {
            return _data.Items.FirstOrDefault(i => i.Id == itemId);
        }

        /// <summary>
        /// Checks if a file is already in favorites.
        /// </summary>
        public bool IsFileFavorited(string filePath)
        {
            var relativePath = ToRelativePath(filePath);
            return _data.Items.Any(i => 
                i.FilePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase) ||
                ToAbsolutePath(i.FilePath).Equals(filePath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks if there are any favorites.
        /// </summary>
        public bool HasFavorites => _data.Items.Any();

        private void RaiseFavoritesChanged()
        {
            FavoritesChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
