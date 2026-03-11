using System.Collections.Generic;
using Microsoft.Internal.VisualStudio.PlatformUI;

namespace SolutionFavorites.MEF
{
    /// <summary>
    /// Handles double-click and Enter key invocations on Favorites nodes.
    /// </summary>
    internal sealed class FavoritesInvocationController : IInvocationController
    {
        private static FavoritesInvocationController _instance;

        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static FavoritesInvocationController Instance =>
            _instance ?? (_instance = new FavoritesInvocationController());

        private FavoritesInvocationController() { }

        public bool Invoke(IEnumerable<object> items, InputSource inputSource, bool preview)
        {
            foreach (var item in items)
            {
                if (item is FavoriteFileNode fileNode)
                {
                    OpenFile(fileNode, preview);
                }
            }

            return true;
        }

        private static void OpenFile(FavoriteFileNode fileNode, bool preview)
        {
            if (!fileNode.FileExists)
            {
                VS.MessageBox.ShowWarning(
                    "File Not Found",
                    $"The file '{fileNode.AbsoluteFilePath}' no longer exists.");
                return;
            }

            if (preview)
            {
                VS.Documents.OpenInPreviewTabAsync(fileNode.AbsoluteFilePath).FireAndForget();
            }
            else
            {
                VS.Documents.OpenAsync(fileNode.AbsoluteFilePath).FireAndForget();
            }
        }
    }
}
