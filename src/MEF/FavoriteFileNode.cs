using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        FavoriteNodeBase,
        ITreeDisplayItemWithImages,
        IPrioritizedComparable,
        IContextMenuPattern,
        IInvocationPattern,
        IDragDropSourcePattern
    {
        // Cache for file icons to avoid repeated expensive IVsImageService2 calls
        private static readonly ConcurrentDictionary<string, ImageMoniker> _fileIconCache = new ConcurrentDictionary<string, ImageMoniker>();

        // Lazy service resolution to avoid repeated service lookups
        private static IVsImageService2 ImageService => _imageService ?? (_imageService = VS.GetRequiredService<SVsImageService, IVsImageService2>());
        private static IVsImageService2 _imageService;

        protected override HashSet<Type> SupportedPatterns { get; } = new HashSet<Type>
        {
            typeof(ITreeDisplayItem),
            typeof(IBrowsablePattern),
            typeof(IContextMenuPattern),
            typeof(IInvocationPattern),
            typeof(IDragDropSourcePattern),
            typeof(ISupportDisposalNotification),
        };

        public FavoriteFileNode(FavoriteItem item, object parent)
            : base(parent)
        {
            Item = item;
        }

        /// <summary>
        /// The underlying favorite item data.
        /// </summary>
        public FavoriteItem Item { get; }

        /// <summary>
        /// Gets the absolute file path.
        /// </summary>
        public string AbsoluteFilePath => FavoritesManager.Instance.ToAbsolutePath(Item.Path);

        /// <summary>
        /// Checks if the file still exists on disk.
        /// </summary>
        public bool FileExists => !string.IsNullOrEmpty(Item.Path) && File.Exists(AbsoluteFilePath);

        // ITreeDisplayItem
        public override string Text => Item.Name;
        public override string ToolTipText => AbsoluteFilePath ?? Item.Name;
        public override string StateToolTipText => FileExists ? string.Empty : "File not found";
        System.Windows.FontStyle ITreeDisplayItem.FontStyle => FileExists ? System.Windows.FontStyles.Normal : System.Windows.FontStyles.Italic;
        public override bool IsCut => !FileExists;

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

        // IPrioritizedComparable - Files appear after folders
        public int Priority => 1;

        public int CompareTo(object obj)
        {
            if (obj is ITreeDisplayItem other)
            {
                return StringComparer.OrdinalIgnoreCase.Compare(Text, other.Text);
            }
            return 0;
        }

        // IInvocationPattern
        public IInvocationController InvocationController => FavoritesInvocationController.Instance;
        public bool CanPreview => FileExists;

        // IDragDropSourcePattern
        public IDragDropSourceController DragDropSourceController => FavoritesDragDropController.Instance;

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
