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
        
        // HashSet for O(1) duplicate file path lookups (stores lowercase relative paths)
        private HashSet<string> _filePathIndex;

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
        public event EventHandler<FavoritesChangedEventArgs> FavoritesChanged;

        private FavoritesManager()
        {
            _data = new FavoritesData();
            _filePathIndex = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates a standalone instance for unit testing (no singleton, no VS dependencies).
        /// </summary>
        internal static FavoritesManager CreateForTesting() => new FavoritesManager();

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
        internal string ToRelativePath(string absolutePath)
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
        /// Stored in the solution directory so it can be committed to source control.
        /// </summary>
        private string GetFavoritesFilePath(string solutionPath)
        {
            if (string.IsNullOrEmpty(solutionPath))
                return null;

            var solutionDir = Path.GetDirectoryName(solutionPath);
            return Path.Combine(solutionDir, "favorites.json");
        }

        /// <summary>
        /// Loads favorites for the given solution.
        /// </summary>
        public void LoadForSolution(string solutionPath)
        {
            _currentSolutionPath = solutionPath;
            _solutionDirectory = Path.GetDirectoryName(solutionPath);
            _data = new FavoritesData();
            _filePathIndex = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var filePath = GetFavoritesFilePath(solutionPath);
            if (filePath != null && File.Exists(filePath))
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    _data = JsonConvert.DeserializeObject<FavoritesData>(json) ?? new FavoritesData();
                    
                    // Sort items after loading (ensures consistent order)
                    SortItemsInPlace(_data.Items);
                    
                    // Build the path index for O(1) lookups
                    RebuildPathIndex();
                }
                catch (Exception)
                {
                    _data = new FavoritesData();
                }
            }

            // Default visibility: visible only if there are favorites
            _isVisible = HasFavorites;

            RaiseFavoritesChanged();
        }

        /// <summary>
        /// Saves the current favorites to disk.
        /// </summary>
        public void Save()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
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
            _filePathIndex = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            RaiseFavoritesChanged();
        }

        /// <summary>
        /// Gets all root-level favorites.
        /// </summary>
        public IReadOnlyList<FavoriteItem> GetRootItems()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            EnsureSolutionPathLoaded();
            // Items are maintained in sorted order, return directly
            return _data.Items;
        }

        /// <summary>
        /// Gets items within a specific folder.
        /// </summary>
        public IReadOnlyList<FavoriteItem> GetFolderItems(FavoriteItem folder)
        {
            if (folder?.Children == null)
                return Array.Empty<FavoriteItem>();

            // Items are maintained in sorted order, return directly
            return folder.Children;
        }

        /// <summary>
        /// Inserts an item into a list maintaining sorted order (folders first, then by name).
        /// </summary>
        internal static void InsertSorted(List<FavoriteItem> items, FavoriteItem newItem)
        {
            var insertIndex = 0;
            
            for (var i = 0; i < items.Count; i++)
            {
                var existing = items[i];
                
                // Folders come before files
                if (newItem.IsFolder && !existing.IsFolder)
                {
                    insertIndex = i;
                    break;
                }
                
                // Files come after folders
                if (!newItem.IsFolder && existing.IsFolder)
                {
                    insertIndex = i + 1;
                    continue;
                }
                
                // Same type: compare by name
                if (StringComparer.OrdinalIgnoreCase.Compare(newItem.Name, existing.Name) < 0)
                {
                    insertIndex = i;
                    break;
                }
                
                insertIndex = i + 1;
            }
            
            items.Insert(insertIndex, newItem);
        }

        /// <summary>
        /// Sorts a list of items in place (folders first, then by name).
        /// Used after loading from disk.
        /// </summary>
        internal static void SortItemsInPlace(List<FavoriteItem> items)
        {
            items.Sort((a, b) =>
            {
                // Folders before files
                var folderCompare = (a.IsFolder ? 0 : 1).CompareTo(b.IsFolder ? 0 : 1);
                if (folderCompare != 0)
                    return folderCompare;
                
                return StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
            });
            
            // Recursively sort children
            foreach (var item in items.Where(i => i.IsFolder && i.Children != null))
            {
                SortItemsInPlace(item.Children);
            }
        }

        /// <summary>
        /// Adds a file to favorites at the root level.
        /// </summary>
        public FavoriteItem AddFile(string filePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            EnsureSolutionPathLoaded();

            var relativePath = ToRelativePath(filePath);

            // Check if file already exists anywhere (O(1) lookup)
            if (FileExistsInTree(relativePath))
            {
                return null;
            }

            var item = FavoriteItem.CreateFile(relativePath);
            InsertSorted(_data.Items, item); // Insert in sorted position
            _filePathIndex.Add(relativePath); // Maintain index
            
            // Auto-show favorites when adding first item
            if (!_isVisible)
            {
                _isVisible = true;
                RaiseVisibilityChanged();
            }
            
            Save();
            RaiseFavoritesChanged(null); // Root affected
            return item;
        }

        /// <summary>
        /// Adds a file to a specific folder.
        /// </summary>
        public FavoriteItem AddFileToFolder(string filePath, FavoriteItem folder)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            EnsureSolutionPathLoaded();

            var relativePath = ToRelativePath(filePath);

            // Check if file already exists anywhere (O(1) lookup)
            if (FileExistsInTree(relativePath))
            {
                return null;
            }

            var item = FavoriteItem.CreateFile(relativePath);
            InsertSorted(folder.Children, item); // Insert in sorted position
            _filePathIndex.Add(relativePath); // Maintain index
            Save();
            RaiseFavoritesChanged(folder); // Specific folder affected
            return item;
        }

        /// <summary>
        /// Creates a new folder at the root level.
        /// </summary>
        public FavoriteItem CreateFolder(string name)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            EnsureSolutionPathLoaded();

            var folder = FavoriteItem.CreateFolder(name);
            InsertSorted(_data.Items, folder); // Insert in sorted position
            
            // Auto-show favorites when adding first item
            if (!_isVisible)
            {
                _isVisible = true;
                RaiseVisibilityChanged();
            }
            
            Save();
            RaiseFavoritesChanged(null); // Root affected
            return folder;
        }

        /// <summary>
        /// Creates a new folder inside an existing folder.
        /// </summary>
        public FavoriteItem CreateFolderIn(string name, FavoriteItem parentFolder)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            EnsureSolutionPathLoaded();

            var folder = FavoriteItem.CreateFolder(name);
            InsertSorted(parentFolder.Children, folder); // Insert in sorted position
            Save();
            RaiseFavoritesChanged(parentFolder); // Parent folder affected
            return folder;
        }

        /// <summary>
        /// Renames a folder.
        /// </summary>
        public void RenameFolder(FavoriteItem folder, string newName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (folder == null || !folder.IsFolder)
                return;

            folder.Name = newName;
            Save();
            RaiseFavoritesChanged(folder); // The folder itself is affected
        }

        /// <summary>
        /// Moves an item to a different location.
        /// </summary>
        public void MoveItem(FavoriteItem item, FavoriteItem targetFolder)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
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

            // Add to new location in sorted position
            if (targetFolder == null)
            {
                InsertSorted(_data.Items, item);
            }
            else
            {
                InsertSorted(targetFolder.Children, item);
            }

            Save();
            RaiseFavoritesChanged(null); // Full refresh needed - items moved between containers
        }

        /// <summary>
        /// Checks if potentialDescendant is nested inside ancestor.
        /// </summary>
        internal bool IsDescendantOf(FavoriteItem potentialDescendant, FavoriteItem ancestor)
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
            ThreadHelper.ThrowIfNotOnUIThread();
            if (item == null)
                return;

            // Remove from path index
            if (!item.IsFolder && !string.IsNullOrEmpty(item.Path))
            {
                _filePathIndex.Remove(item.Path);
            }
            else if (item.IsFolder)
            {
                // Remove all nested file paths from index
                RemoveFromPathIndex(item.Children);
            }

            RemoveFromTree(_data.Items, item);
            Save();
            var affectedFolder = FindParentFolder(item);
            RaiseFavoritesChanged(affectedFolder);
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
        /// Checks if a file path exists anywhere in the tree using the path index (O(1)).
        /// </summary>
        private bool FileExistsInTree(string relativePath)
        {
            return _filePathIndex.Contains(relativePath);
        }

        /// <summary>
        /// Rebuilds the path index from the current data.
        /// </summary>
        private void RebuildPathIndex()
        {
            _filePathIndex.Clear();
            AddToPathIndex(_data.Items);
        }

        /// <summary>
        /// Recursively adds all file paths to the index.
        /// </summary>
        private void AddToPathIndex(List<FavoriteItem> items)
        {
            foreach (var item in items)
            {
                if (!item.IsFolder && !string.IsNullOrEmpty(item.Path))
                {
                    _filePathIndex.Add(item.Path);
                }
                else if (item.IsFolder && item.Children != null)
                {
                    AddToPathIndex(item.Children);
                }
            }
        }

        /// <summary>
        /// Recursively removes all file paths from the index.
        /// </summary>
        private void RemoveFromPathIndex(List<FavoriteItem> items)
        {
            if (items == null)
                return;

            foreach (var item in items)
            {
                if (!item.IsFolder && !string.IsNullOrEmpty(item.Path))
                {
                    _filePathIndex.Remove(item.Path);
                }
                else if (item.IsFolder && item.Children != null)
                {
                    RemoveFromPathIndex(item.Children);
                }
            }
        }

        /// <summary>
        /// Checks if a file is already in favorites.
        /// </summary>
        public bool IsFileFavorited(string filePath)
        {
            var relativePath = ToRelativePath(filePath);
            return FileExistsInTree(relativePath);
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
        private bool _isVisible;

        /// <summary>
        /// Event raised when visibility changes.
        /// </summary>
        public event EventHandler VisibilityChanged;

        private void RaiseFavoritesChanged(FavoriteItem affectedFolder = null)
        {
            FavoritesChanged?.Invoke(this, new FavoritesChangedEventArgs(affectedFolder));
        }

        private void RaiseVisibilityChanged()
        {
            VisibilityChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Checks if there are duplicate filenames in the same folder.
        /// </summary>
        public bool HasDuplicateNames(FavoriteItem fileItem)
        {
            if (fileItem.IsFolder || string.IsNullOrEmpty(fileItem.Path))
                return false;

            FavoriteItem folder = FindParentFolder(fileItem);
            var siblings = folder?.Children ?? _data.Items;

            int count = 0;
            foreach (var sibling in siblings)
            {
                if (!sibling.IsFolder && sibling.Name == fileItem.Name)
                {
                    count++;
                    if (count > 1) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Finds the parent folder of a given item.
        /// </summary>
        private FavoriteItem FindParentFolder(FavoriteItem item)
        {
            foreach (var root in _data.Items)
            {
                if (root.Children?.Contains(item) == true) return root;
                var found = FindInChildren(root, item);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Recursively searches for the parent of the target item.
        /// </summary>
        private FavoriteItem FindInChildren(FavoriteItem parent, FavoriteItem target)
        {
            if (parent.Children?.Contains(target) == true) return parent;
            foreach (var child in parent.Children ?? new List<FavoriteItem>())
            {
                if (child.IsFolder)
                {
                    var found = FindInChildren(child, target);
                    if (found != null) return found;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Event args for favorites changed events.
    /// </summary>
    internal sealed class FavoritesChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The folder that was affected, or null if the root was affected or a full refresh is needed.
        /// </summary>
        public FavoriteItem AffectedFolder { get; }

        public FavoritesChangedEventArgs(FavoriteItem affectedFolder)
        {
            AffectedFolder = affectedFolder;
        }
    }
}
