using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using SolutionFavorites.Models;

namespace SolutionFavorites.MEF
{
    /// <summary>
    /// Represents a favorited file node in the Favorites tree.
    /// </summary>
    internal sealed class FavoriteFileNode :
        ITreeDisplayItem,
        ITreeDisplayItemWithImages,
        IPrioritizedComparable,
        IBrowsablePattern,
        IInteractionPatternProvider,
        IContextMenuPattern,
        IInvocationPattern,
        ISupportDisposalNotification,
        INotifyPropertyChanged,
        IDisposable
    {
        private bool _disposed;

        // Cache for file icons to avoid repeated expensive IVsImageService2 calls
        private static readonly ConcurrentDictionary<string, ImageMoniker> _fileIconCache = new ConcurrentDictionary<string, ImageMoniker>();

        // Lazy service resolution to avoid repeated service lookups
        private static IVsImageService2 ImageService => _imageService ?? (_imageService = VS.GetRequiredService<SVsImageService, IVsImageService2>());
        private static IVsImageService2 _imageService;

        private static readonly HashSet<Type> _supportedPatterns = new HashSet<Type>
        {
            typeof(ITreeDisplayItem),
            typeof(IBrowsablePattern),
            typeof(IContextMenuPattern),
            typeof(IInvocationPattern),
            typeof(ISupportDisposalNotification),
        };

        public FavoriteFileNode(FavoriteItem item, object parent)
        {
            Item = item;
            SourceItem = parent;
        }

        /// <summary>
        /// The underlying favorite item data.
        /// </summary>
        public FavoriteItem Item { get; }

        /// <summary>
        /// Parent node.
        /// </summary>
        public object SourceItem { get; }

        /// <summary>
        /// Gets the absolute file path.
        /// </summary>
        public string AbsoluteFilePath => FavoritesManager.Instance.ToAbsolutePath(Item.FilePath);

        /// <summary>
        /// Checks if the file still exists on disk.
        /// </summary>
        public bool FileExists => !string.IsNullOrEmpty(Item.FilePath) && File.Exists(AbsoluteFilePath);

        // ITreeDisplayItem
        public string Text => Item.Name;
        public string ToolTipText => AbsoluteFilePath ?? Item.Name;
        public object ToolTipContent => ToolTipText;
        public string StateToolTipText => FileExists ? string.Empty : "File not found";
        System.Windows.FontWeight ITreeDisplayItem.FontWeight => System.Windows.FontWeights.Normal;
        System.Windows.FontStyle ITreeDisplayItem.FontStyle => FileExists ? System.Windows.FontStyles.Normal : System.Windows.FontStyles.Italic;
        public bool IsCut => !FileExists;

        // ITreeDisplayItemWithImages
        public ImageMoniker IconMoniker
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                
                if (!FileExists)
                    return KnownMonikers.DocumentWarning;

                return GetFileIcon(AbsoluteFilePath);
            }
        }

        public ImageMoniker ExpandedIconMoniker
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return IconMoniker;
            }
        }

        public ImageMoniker OverlayIconMoniker => default;
        public ImageMoniker StateIconMoniker => FileExists ? default : KnownMonikers.StatusWarning;

        // IPrioritizedComparable
        public int Priority => 1;

        public int CompareTo(object obj)
        {
            if (obj is ITreeDisplayItem other)
            {
                return StringComparer.OrdinalIgnoreCase.Compare(Text, other.Text);
            }
            return 0;
        }

        // IBrowsablePattern
        public object GetBrowseObject() => this;

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

        // IInvocationPattern
        public IInvocationController InvocationController => FavoritesInvocationController.Instance;
        public bool CanPreview => FileExists;

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
                RaisePropertyChanged(nameof(IsDisposed));
            }
        }


        private static ImageMoniker GetFileIcon(string filePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Use file extension as cache key for better cache efficiency
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var cacheKey = string.IsNullOrEmpty(extension) ? Path.GetFileName(filePath).ToLowerInvariant() : extension;

            return _fileIconCache.GetOrAdd(cacheKey, _ =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                ImageMoniker moniker = ImageService.GetImageMonikerForFile(filePath);
                return moniker.Id < 0 ? KnownMonikers.Document : moniker;
            });
        }
    }
}
