using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using Microsoft.Internal.VisualStudio.PlatformUI;
using SolutionFavorites.Models;

namespace SolutionFavorites.MEF
{
    /// <summary>
    /// Base class for all favorite tree nodes, providing shared functionality for
    /// disposal notification, property change notification, and pattern support.
    /// </summary>
    internal abstract class FavoriteNodeBase :
        ITreeDisplayItem,
        IBrowsablePattern,
        IInteractionPatternProvider,
        IContextMenuPattern,
        ISupportDisposalNotification,
        INotifyPropertyChanged,
        IDisposable
    {

        /// <summary>
        /// The set of pattern types this node supports.
        /// Derived classes should override to add additional patterns.
        /// </summary>
        protected virtual HashSet<Type> SupportedPatterns { get; } =
        [
            typeof(ITreeDisplayItem),
            typeof(IBrowsablePattern),
            typeof(IContextMenuPattern),
            typeof(ISupportDisposalNotification),
        ];

        /// <summary>
        /// Parent node in the tree.
        /// </summary>
        public object SourceItem { get; }

        protected FavoriteNodeBase(object parent)
        {
            SourceItem = parent;
        }

        // ITreeDisplayItem - abstract properties for derived classes to implement
        public abstract string Text { get; }
        public virtual string ToolTipText => Text;
        public virtual object ToolTipContent => ToolTipText;
        public virtual string StateToolTipText => string.Empty;
        public virtual FontWeight FontWeight => FontWeights.Normal;
        System.Windows.FontStyle ITreeDisplayItem.FontStyle => System.Windows.FontStyles.Normal;
        public virtual bool IsCut => false;

        // IBrowsablePattern
        public object GetBrowseObject() => this;

        // IContextMenuPattern
        public IContextMenuController ContextMenuController => FavoritesContextMenuController.Instance;

        // IInteractionPatternProvider
        public virtual TPattern GetPattern<TPattern>() where TPattern : class
        {
            if (!IsDisposed && SupportedPatterns.Contains(typeof(TPattern)))
            {
                return this as TPattern;
            }

            return typeof(TPattern) == typeof(ISupportDisposalNotification) ? this as TPattern : null;
        }

        // ISupportDisposalNotification
        public bool IsDisposed { get; private set; }

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Called when the node is being disposed. Override to add cleanup logic.
        /// </summary>
        protected virtual void OnDisposing() { }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                OnDisposing();
                RaisePropertyChanged(nameof(IsDisposed));
            }
        }

        #region Drag-Drop Target Helpers

        /// <summary>
        /// Checks if the drag data contains items that can be added to favorites.
        /// </summary>
        private static bool CanAcceptDrop(DragEventArgs e)
        {
            // Accept our internal favorites format (for reordering)
            if (e.Data.GetDataPresent(DragDropFormats.Favorites))
            {
                return true;
            }

            // Accept file drops from Solution Explorer or Windows Explorer
            return e.Data.GetDataPresent(DataFormats.FileDrop);
        }

        /// <summary>
        /// Handles drag enter for favorites items.
        /// </summary>
        protected static void HandleDragEnter(DragEventArgs e)
        {
            if (CanAcceptDrop(e))
            {
                e.Effects = e.Data.GetDataPresent(DragDropFormats.Favorites)
                    ? DragDropEffects.Move
                    : DragDropEffects.Copy;
            }
        }

        /// <summary>
        /// Handles drag over for favorites items.
        /// </summary>
        protected static void HandleDragOver(DragEventArgs e)
        {
            if (CanAcceptDrop(e))
            {
                e.Effects = e.Data.GetDataPresent(DragDropFormats.Favorites)
                    ? DragDropEffects.Move
                    : DragDropEffects.Copy;
            }
        }

        /// <summary>
        /// Handles drag leave for favorites items.
        /// </summary>
        protected static void HandleDragLeave(DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
        }

        /// <summary>
        /// Handles drop for favorites items, moving them to the target folder.
        /// </summary>
        /// <param name="targetFolder">The target folder, or null for root level.</param>
        /// <param name="e">The drag event args.</param>
        protected static void HandleDrop(FavoriteItem targetFolder, DragEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Handle internal favorites reordering
            if (e.Data.GetDataPresent(DragDropFormats.Favorites))
            {
                HandleFavoritesDrop(targetFolder, e);
                return;
            }

            // Handle file drops from Solution Explorer or Windows Explorer
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                HandleFileDrop(targetFolder, e);
                return;
            }
        }

        /// <summary>
        /// Handles dropping favorites items for reordering.
        /// </summary>
        private static void HandleFavoritesDrop(FavoriteItem targetFolder, DragEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (e.Data.GetData(DragDropFormats.Favorites) is not object[] nodes)
            {
                return;
            }

            foreach (var node in nodes)
            {
                FavoriteItem itemToMove = null;

                if (node is FavoriteFileNode fileNode)
                {
                    itemToMove = fileNode.Item;
                }
                else if (node is FavoriteFolderNode folderNode)
                {
                    // Don't allow dropping a folder onto itself
                    if (targetFolder != null && folderNode.Item == targetFolder)
                    {
                        continue;
                    }

                    itemToMove = folderNode.Item;
                }

                if (itemToMove != null)
                {
                    FavoritesManager.Instance.MoveItem(itemToMove, targetFolder);
                }
            }

            e.Handled = true;
        }

        /// <summary>
        /// Handles dropping files from Solution Explorer or Windows Explorer.
        /// </summary>
        private static void HandleFileDrop(FavoriteItem targetFolder, DragEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            {
                return;
            }

            foreach (var filePath in files)
            {
                // Only add files, not directories
                if (System.IO.File.Exists(filePath))
                {
                    if (targetFolder != null)
                    {
                        _ = FavoritesManager.Instance.AddFileToFolder(filePath, targetFolder);
                    }
                    else
                    {
                        _ = FavoritesManager.Instance.AddFile(filePath);
                    }
                }
            }

            e.Handled = true;
        }

        #endregion

        #region Child Collection Helpers

        /// <summary>
        /// Disposes all children in a collection and clears it.
        /// </summary>
        protected static void DisposeChildren(ObservableCollection<object> children)
        {
            foreach (var child in children)
            {
                (child as IDisposable)?.Dispose();
            }
            children.Clear();
        }

        /// <summary>
        /// Performs a smart refresh of children that preserves existing folder nodes (and their expanded state).
        /// Only adds new items and removes deleted ones, without clearing the collection.
        /// </summary>
        /// <param name="children">The observable collection of child nodes.</param>
        /// <param name="currentItems">The current list of favorite items from the data model.</param>
        /// <param name="parent">The parent node for newly created nodes.</param>
        protected static void SmartRefreshChildren(ObservableCollection<object> children, IReadOnlyList<FavoriteItem> currentItems, object parent)
        {
            // Build lookup of existing nodes by their FavoriteItem
            var existingNodes = new Dictionary<FavoriteItem, object>();
            foreach (var child in children)
            {
                if (child is FavoriteFileNode fileNode)
                {
                    existingNodes[fileNode.Item] = child;
                }
                else if (child is FavoriteFolderNode folderNode)
                {
                    existingNodes[folderNode.Item] = child;
                }
            }

            // Build set of current items for quick lookup
            var currentItemSet = new HashSet<FavoriteItem>(currentItems);

            // Remove nodes that are no longer in the data model (iterate backwards to safely remove)
            for (var i = children.Count - 1; i >= 0; i--)
            {
                var child = children[i];
                FavoriteItem childItem = null;

                if (child is FavoriteFileNode fileNode)
                {
                    childItem = fileNode.Item;
                }
                else if (child is FavoriteFolderNode folderNode)
                {
                    childItem = folderNode.Item;
                }

                if (childItem != null && !currentItemSet.Contains(childItem))
                {
                    children.RemoveAt(i);
                    (child as IDisposable)?.Dispose();
                }
            }

            // Now sync the collection to match the order in currentItems
            // Add missing items and reorder existing ones
            for (var i = 0; i < currentItems.Count; i++)
            {
                FavoriteItem item = currentItems[i];

                // Check if we already have a node for this item
                if (existingNodes.TryGetValue(item, out var existingNode))
                {
                    // Find where this node currently is in the collection
                    var currentIndex = children.IndexOf(existingNode);

                    if (currentIndex == -1)
                    {
                        // Node was removed (shouldn't happen, but handle it)
                        children.Insert(i, existingNode);
                    }
                    else if (currentIndex != i)
                    {
                        // Node exists but is in wrong position - move it
                        children.Move(currentIndex, i);
                    }
                    // else: node is already in correct position, do nothing
                }
                else
                {
                    // Need to create a new node for this item
                    var newNode = CreateNodeForItem(item, parent);
                    children.Insert(i, newNode);
                }
            }
        }

        /// <summary>
        /// Creates the appropriate node type for a favorite item.
        /// </summary>
        protected static object CreateNodeForItem(FavoriteItem item, object parent)
        {
            return item.IsFolder
                ? new FavoriteFolderNode(item, parent)
                : new FavoriteFileNode(item, parent);
        }

        #endregion
    }
}
