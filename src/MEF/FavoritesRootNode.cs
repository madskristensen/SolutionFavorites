using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using SolutionFavorites.Models;

namespace SolutionFavorites.MEF
{
    /// <summary>
    /// The root "Favorites" node shown as the first child under the solution.
    /// </summary>
    internal sealed class FavoritesRootNode : 
        IAttachedCollectionSource,
        ITreeDisplayItem,
        ITreeDisplayItemWithImages,
        IPrioritizedComparable,
        IBrowsablePattern,
        IInteractionPatternProvider,
        IContextMenuPattern,
        ISupportDisposalNotification,
        INotifyPropertyChanged,
        IDisposable
    {
        private readonly ObservableCollection<object> _children;
        private readonly object _sourceItem;
        private bool _disposed;

        private static readonly HashSet<Type> _supportedPatterns = new HashSet<Type>
        {
            typeof(ITreeDisplayItem),
            typeof(IBrowsablePattern),
            typeof(IContextMenuPattern),
            typeof(ISupportDisposalNotification),
        };

        public FavoritesRootNode(object sourceItem)
        {
            _sourceItem = sourceItem;
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
            _children.Clear();

            var rootItems = FavoritesManager.Instance.GetRootItems();
            foreach (var item in rootItems)
            {
                _children.Add(new FavoriteFileNode(item, this));
            }

            RaisePropertyChanged(nameof(HasItems));
        }

        // IAttachedCollectionSource
        public object SourceItem => _sourceItem;
        public bool HasItems => _children.Count > 0;
        public IEnumerable Items => _children;

        // IBrowsablePattern
        public object GetBrowseObject() => this;

        // ITreeDisplayItem
        public string Text => "Favorites";
        public string ToolTipText => "Favorite files pinned for quick access";
        public object ToolTipContent => ToolTipText;
        public string StateToolTipText => string.Empty;
        System.Windows.FontWeight ITreeDisplayItem.FontWeight => System.Windows.FontWeights.Bold;
        System.Windows.FontStyle ITreeDisplayItem.FontStyle => System.Windows.FontStyles.Normal;
        public bool IsCut => false;

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

        // IInteractionPatternProvider
        public TPattern GetPattern<TPattern>() where TPattern : class
        {
            if (!_disposed && _supportedPatterns.Contains(typeof(TPattern)))
            {
                return this as TPattern;
            }

            if (typeof(TPattern) == typeof(ISupportDisposalNotification))
            {
                return this as TPattern;
            }

            return null;
        }

        // IContextMenuPattern
        public IContextMenuController ContextMenuController => FavoritesContextMenuController.Instance;

        // ISupportDisposalNotification
        public bool IsDisposed => _disposed;

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                FavoritesManager.Instance.FavoritesChanged -= OnFavoritesChanged;
                _children.Clear();
                RaisePropertyChanged(nameof(IsDisposed));
            }
        }
    }
}
