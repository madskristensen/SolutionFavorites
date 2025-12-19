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
            _items = new ObservableCollection<object> { _rootNode };
        }

        public object SourceItem { get; }

        public bool HasItems => _items.Count > 0;

        public IEnumerable Items => _items;

        public event PropertyChangedEventHandler PropertyChanged;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _items.Clear();
            }
        }
    }
}
