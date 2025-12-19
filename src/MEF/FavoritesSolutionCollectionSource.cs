using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.Internal.VisualStudio.PlatformUI;

namespace SolutionFavorites.MEF
{
    /// <summary>
    /// Collection source that provides the Favorites root node as a child of the solution node.
    /// This wrapper is needed so that the FavoritesRootNode appears as a child of the solution,
    /// rather than its contents appearing directly under the solution.
    /// </summary>
    internal sealed class FavoritesSolutionCollectionSource : IAttachedCollectionSource, INotifyPropertyChanged, IDisposable
    {
        private readonly ObservableCollection<object> _items;
        private readonly FavoritesRootNode _rootNode;
        private bool _disposed;

        public FavoritesSolutionCollectionSource(object sourceItem, FavoritesRootNode rootNode)
        {
            SourceItem = sourceItem;
            _rootNode = rootNode;
            _items = new ObservableCollection<object>();
            
            // Listen for favorites and visibility changes
            FavoritesManager.Instance.FavoritesChanged += OnFavoritesChanged;
            FavoritesManager.Instance.VisibilityChanged += OnVisibilityChanged;
            
            // Initial visibility check
            UpdateRootNodeVisibility();
        }

        private void OnFavoritesChanged(object sender, EventArgs e)
        {
            UpdateRootNodeVisibility();
        }

        private void OnVisibilityChanged(object sender, EventArgs e)
        {
            UpdateRootNodeVisibility();
        }

        private void UpdateRootNodeVisibility()
        {
            var shouldShow = FavoritesManager.Instance.IsVisible && FavoritesManager.Instance.HasFavorites;
            
            if (shouldShow && !_items.Contains(_rootNode))
            {
                _items.Add(_rootNode);
                RaisePropertyChanged(nameof(HasItems));
            }
            else if (!shouldShow && _items.Contains(_rootNode))
            {
                _items.Remove(_rootNode);
                RaisePropertyChanged(nameof(HasItems));
            }
        }

        public object SourceItem { get; }

        public bool HasItems => _items.Count > 0;

        public IEnumerable Items => _items;

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
                FavoritesManager.Instance.VisibilityChanged -= OnVisibilityChanged;
                _items.Clear();
            }
        }
    }
}
