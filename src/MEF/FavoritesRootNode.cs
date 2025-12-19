using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;

namespace SolutionFavorites.MEF
{
    /// <summary>
    /// The root "Favorites" node shown as the first child under the solution.
    /// </summary>
    internal sealed class FavoritesRootNode : 
        FavoriteNodeBase,
        IAttachedCollectionSource,
        ITreeDisplayItemWithImages,
        IPrioritizedComparable,
        IContextMenuPattern,
        IDragDropTargetPattern
    {
        private readonly ObservableCollection<object> _children;

        protected override HashSet<Type> SupportedPatterns { get; } = new HashSet<Type>
        {
            typeof(ITreeDisplayItem),
            typeof(IBrowsablePattern),
            typeof(IContextMenuPattern),
            typeof(IDragDropTargetPattern),
            typeof(ISupportDisposalNotification),
        };

        public FavoritesRootNode(object sourceItem)
            : base(sourceItem)
        {
            _children = new ObservableCollection<object>();
            FavoritesManager.Instance.FavoritesChanged += OnFavoritesChanged;
            
            // Do initial refresh
            RefreshChildren();
        }

        private void OnFavoritesChanged(object sender, EventArgs e)
        {
#pragma warning disable VSTHRD110 // Observe result of async calls
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                RefreshChildren();
            });
#pragma warning restore VSTHRD110
        }

        private void RefreshChildren()
        {
            DisposeChildren(_children);

            var rootItems = FavoritesManager.Instance.GetRootItems();
            foreach (var item in rootItems)
            {
                _children.Add(CreateNodeForItem(item, this));
            }

            RaisePropertyChanged(nameof(HasItems));
            RaisePropertyChanged(nameof(Items));
        }

        // IAttachedCollectionSource
        public bool HasItems => FavoritesManager.Instance.HasFavorites;
        public IEnumerable Items => _children;

        // ITreeDisplayItem
        public override string Text => "Favorites";
        public override string ToolTipText => "Favorite files pinned for quick access";
        public override FontWeight FontWeight => FontWeights.Bold;

        // ITreeDisplayItemWithImages
        public ImageMoniker IconMoniker => KnownMonikers.Favorite;
        public ImageMoniker ExpandedIconMoniker => KnownMonikers.Favorite;
        public ImageMoniker OverlayIconMoniker => default;
        public ImageMoniker StateIconMoniker => default;

        // IPrioritizedComparable - Priority -1 ensures this appears first
        public int Priority => -1;

        public int CompareTo(object obj)
        {
            if (obj is IPrioritizedComparable other)
            {
                return Priority.CompareTo(other.Priority);
            }
            return -1; // Always sort before non-prioritized items
        }

        // IDragDropTargetPattern
        public DirectionalDropArea SupportedAreas => DirectionalDropArea.On;

        public void OnDragEnter(DirectionalDropArea dropArea, DragEventArgs e) => HandleDragEnter(e);
        public void OnDragOver(DirectionalDropArea dropArea, DragEventArgs e) => HandleDragOver(e);
        public void OnDragLeave(DirectionalDropArea dropArea, DragEventArgs e) => HandleDragLeave(e);
        public void OnDrop(DirectionalDropArea dropArea, DragEventArgs e) => HandleDrop(null, e);

        protected override void OnDisposing()
        {
            FavoritesManager.Instance.FavoritesChanged -= OnFavoritesChanged;
            DisposeChildren(_children);
        }
    }
}
