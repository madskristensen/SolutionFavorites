using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using SolutionFavorites.Models;

namespace SolutionFavorites.MEF
{
    /// <summary>
    /// Represents a virtual folder node in the Favorites tree.
    /// </summary>
    internal sealed class FavoriteFolderNode :
        FavoriteNodeBase,
        IAttachedCollectionSource,
        ITreeDisplayItemWithImages,
        IPrioritizedComparable,
        IContextMenuPattern,
        IInvocationPattern,
        IDragDropSourcePattern,
        IDragDropTargetPattern
    {
        private readonly ObservableCollection<object> _children;
        private bool _isExpanded;

        protected override HashSet<Type> SupportedPatterns { get; } = new HashSet<Type>
        {
            typeof(ITreeDisplayItem),
            typeof(IBrowsablePattern),
            typeof(IContextMenuPattern),
            typeof(IInvocationPattern),
            typeof(IDragDropSourcePattern),
            typeof(IDragDropTargetPattern),
            typeof(ISupportDisposalNotification),
        };

        public FavoriteFolderNode(FavoriteItem item, object parent)
            : base(parent)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Item = item;
            _children = new ObservableCollection<object>();
            FavoritesManager.Instance.FavoritesChanged += OnFavoritesChanged;
            RefreshChildren();
        }

        /// <summary>
        /// The underlying favorite folder item.
        /// </summary>
        public FavoriteItem Item { get; }

        private void OnFavoritesChanged(object sender, FavoritesChangedEventArgs e)
        {
            // Only refresh if this folder was affected, or if a full refresh is needed (null)
            if (e.AffectedFolder != null && e.AffectedFolder != Item)
                return;

#pragma warning disable VSSDK007 // Fire-and-forget is intentional for UI refresh event
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                RefreshChildren();
                
                // Notify UI that the folder's text (name) may have changed
                RaisePropertyChanged(nameof(Text));
            });
#pragma warning restore VSSDK007
        }

        /// <summary>
        /// Refreshes the children of this folder.
        /// </summary>
        public void RefreshChildren()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            
            var folderItems = FavoritesManager.Instance.GetFolderItems(Item);
            SmartRefreshChildren(_children, folderItems, this);

            RaisePropertyChanged(nameof(HasItems));
            RaisePropertyChanged(nameof(Items));
        }

        // IAttachedCollectionSource
        public bool HasItems => Item.Children != null && Item.Children.Count > 0;
        public IEnumerable Items => _children;

        // ITreeDisplayItem
        public override string Text => Item.Name;

        // ITreeDisplayItemWithImages
        public ImageMoniker IconMoniker => _isExpanded ? KnownMonikers.FolderOpened : KnownMonikers.FolderClosed;
        public ImageMoniker ExpandedIconMoniker => KnownMonikers.FolderOpened;
        public ImageMoniker OverlayIconMoniker => default;
        public ImageMoniker StateIconMoniker => default;

        // IPrioritizedComparable - Folders appear before files
        public int Priority => 0;

        public int CompareTo(object obj)
        {
            if (obj is FavoriteFolderNode otherFolder)
            {
                return StringComparer.OrdinalIgnoreCase.Compare(Text, otherFolder.Text);
            }
            if (obj is FavoriteFileNode)
            {
                return -1; // Folders before files
            }
            return 0;
        }

        // IInvocationPattern - double-click expands/collapses folder
        public IInvocationController InvocationController => null;
        public bool CanPreview => false;

        // IDragDropSourcePattern
        public IDragDropSourceController DragDropSourceController => FavoritesDragDropController.Instance;

        // IDragDropTargetPattern
        public DirectionalDropArea SupportedAreas => DirectionalDropArea.On;

        public void OnDragEnter(DirectionalDropArea dropArea, DragEventArgs e) => HandleDragEnter(e);
        public void OnDragOver(DirectionalDropArea dropArea, DragEventArgs e) => HandleDragOver(e);
        public void OnDragLeave(DirectionalDropArea dropArea, DragEventArgs e) => HandleDragLeave(e);
        public void OnDrop(DirectionalDropArea dropArea, DragEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            HandleDrop(Item, e);
        }

        /// <summary>
        /// Updates the expanded state for icon changes.
        /// </summary>
        public void SetExpanded(bool expanded)
        {
            if (_isExpanded != expanded)
            {
                _isExpanded = expanded;
                RaisePropertyChanged(nameof(IconMoniker));
            }
        }

        protected override void OnDisposing()
        {
            FavoritesManager.Instance.FavoritesChanged -= OnFavoritesChanged;
            DisposeChildren(_children);
        }
    }
}
