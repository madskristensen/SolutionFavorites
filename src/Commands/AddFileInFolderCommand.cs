using SolutionFavorites.Helpers;
using SolutionFavorites.MEF;

namespace SolutionFavorites.Commands
{
    /// <summary>
    /// Command to add a file from disk to a specific folder in Favorites.
    /// </summary>
    [Command(PackageIds.AddFileInFolder)]
    internal sealed class AddFileInFolderCommand : BaseCommand<AddFileInFolderCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var currentItem = FavoritesContextMenuController.CurrentItem;

            if (currentItem is FavoriteFolderNode folderNode)
            {
                foreach (var filePath in FileDialogHelper.BrowseForFiles())
                {
                    FavoritesManager.Instance.AddFileToFolder(filePath, folderNode.Item);
                }
            }
        }
    }
}
