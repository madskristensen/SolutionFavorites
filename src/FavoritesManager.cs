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
    /// Manages persistence and operations for favorite files using hierarchical structure.
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
            return SortItems(_data.Items);
        }

        /// <summary>
        /// Gets items within a specific folder.
        /// </summary>
        public IReadOnlyList<FavoriteItem> GetFolderItems(FavoriteItem folder)
        {
            if (folder?.Children == null)
                return new List<FavoriteItem>();

            return SortItems(folder.Children);
        }

        /// <summary>
        /// Sorts items with folders first, then by name.
        /// </summary>
        private IReadOnlyList<FavoriteItem> SortItems(List<FavoriteItem> items)
        {
            return items
                .OrderBy(i => i.IsFolder ? 0 : 1)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Adds a file to favorites at the root level.
        /// </summary>
        public FavoriteItem AddFile(string filePath)
        {
            EnsureSolutionPathLoaded();

            var relativePath = ToRelativePath(filePath);

            // Check if file already exists anywhere
            if (FileExistsInTree(_data.Items, relativePath))
            {
                return null;
            }

            var item = FavoriteItem.CreateFile(relativePath);
            _data.Items.Add(item);
            Save();
            RaiseFavoritesChanged();
            return item;
        }

        /// <summary>
        /// Adds a file to a specific folder.
        /// </summary>
        public FavoriteItem AddFileToFolder(string filePath, FavoriteItem folder)
        {
            EnsureSolutionPathLoaded();

            var relativePath = ToRelativePath(filePath);

            // Check if file already exists anywhere
            if (FileExistsInTree(_data.Items, relativePath))
            {
                return null;
            }

            var item = FavoriteItem.CreateFile(relativePath);
            folder.Children.Add(item);
            Save();
            RaiseFavoritesChanged();
            return item;
        }

        /// <summary>
        /// Creates a new folder at the root level.
        /// </summary>
        public FavoriteItem CreateFolder(string name)
        {
            EnsureSolutionPathLoaded();

            var folder = FavoriteItem.CreateFolder(name);
            _data.Items.Add(folder);
            Save();
            RaiseFavoritesChanged();
            return folder;
        }

        /// <summary>
        /// Creates a new folder inside an existing folder.
        /// </summary>
        public FavoriteItem CreateFolderIn(string name, FavoriteItem parentFolder)
        {
            EnsureSolutionPathLoaded();

            var folder = FavoriteItem.CreateFolder(name);
            parentFolder.Children.Add(folder);
            Save();
            RaiseFavoritesChanged();
            return folder;
        }

        /// <summary>
        /// Renames a folder.
        /// </summary>
        public void RenameFolder(FavoriteItem folder, string newName)
        {
            if (folder == null || !folder.IsFolder)
                return;

            folder.Name = newName;
            Save();
            RaiseFavoritesChanged();
        }

        /// <summary>
        /// Moves an item to a different location.
        /// </summary>
        public void MoveItem(FavoriteItem item, FavoriteItem targetFolder)
        {
            if (item == null)
                return;

            // Prevent moving a folder into itself or its descendants
            if (item.IsFolder && targetFolder != null)
            {
                if (item == targetFolder || IsDescendantOf(targetFolder, item))
                {
                    return;
                }
            }

            // Remove from current location
            RemoveFromTree(_data.Items, item);

            // Add to new location
            if (targetFolder == null)
            {
                _data.Items.Add(item);
            }
            else
            {
                targetFolder.Children.Add(item);
            }

            Save();
            RaiseFavoritesChanged();
        }

        /// <summary>
        /// Checks if potentialDescendant is nested inside ancestor.
        /// </summary>
        private bool IsDescendantOf(FavoriteItem potentialDescendant, FavoriteItem ancestor)
        {
            if (ancestor?.Children == null)
                return false;

            foreach (var child in ancestor.Children)
            {
                if (child == potentialDescendant)
                    return true;

                if (child.IsFolder && IsDescendantOf(potentialDescendant, child))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Removes an item from the tree.
        /// </summary>
        public void Remove(FavoriteItem item)
        {
            if (item == null)
                return;

            RemoveFromTree(_data.Items, item);
            Save();
            RaiseFavoritesChanged();
        }

        /// <summary>
        /// Recursively removes an item from a list.
        /// </summary>
        private bool RemoveFromTree(List<FavoriteItem> items, FavoriteItem itemToRemove)
        {
            if (items.Remove(itemToRemove))
                return true;

            foreach (var folder in items.Where(i => i.IsFolder))
            {
                if (RemoveFromTree(folder.Children, itemToRemove))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a file path exists anywhere in the tree.
        /// </summary>
        private bool FileExistsInTree(List<FavoriteItem> items, string relativePath)
        {
            foreach (var item in items)
            {
                if (!item.IsFolder)
                {
                    if (item.Path != null && item.Path.Equals(relativePath, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                else if (FileExistsInTree(item.Children, relativePath))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if a file is already in favorites.
        /// </summary>
        public bool IsFileFavorited(string filePath)
        {
            var relativePath = ToRelativePath(filePath);
            return FileExistsInTree(_data.Items, relativePath);
        }

        /// <summary>
        /// Checks if there are any favorites.
        /// </summary>
        public bool HasFavorites => _data.Items.Any();

        /// <summary>
        /// Gets or sets whether the Favorites node is visible in Solution Explorer.
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    RaiseVisibilityChanged();
                }
            }
        }
        private bool _isVisible = true;

        /// <summary>
        /// Event raised when visibility changes.
        /// </summary>
        public event EventHandler VisibilityChanged;

        private void RaiseFavoritesChanged()
        {
            FavoritesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RaiseVisibilityChanged()
        {
            VisibilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
