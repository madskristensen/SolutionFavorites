using SolutionFavorites.MEF;

using System.Windows;

namespace SolutionFavorites.Commands
{
    /// <summary>
    /// Command to copy the full path of a favorite file.
    /// </summary>
    [Command(PackageIds.CopyFullPath)]
    internal sealed class CopyFullPathCommand : BaseCommand<CopyFullPathCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var currentItem = FavoritesContextMenuController.CurrentItem;

            if (currentItem is FavoriteFileNode fileNode)
            {
                var filePath = fileNode.AbsoluteFilePath;
                if (!string.IsNullOrEmpty(filePath))
                {
                    Clipboard.SetText(filePath);
                }
            }
        }
    }
}
