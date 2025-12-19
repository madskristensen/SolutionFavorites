using SolutionFavorites.MEF;

namespace SolutionFavorites.Commands
{
    /// <summary>
    /// Command to open the file location in Windows Explorer.
    /// </summary>
    [Command(PackageIds.OpenContainingFolder)]
    internal sealed class OpenContainingFolderCommand : BaseCommand<OpenContainingFolderCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var currentItem = FavoritesContextMenuController.CurrentItem;

            if (currentItem is FavoriteFileNode fileNode && fileNode.FileExists)
            {
                var filePath = fileNode.AbsoluteFilePath;
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
        }
    }
}
