using SolutionFavorites.MEF;

namespace SolutionFavorites.Commands
{
    /// <summary>
    /// Command to create a new subfolder inside an existing folder.
    /// </summary>
    [Command(PackageIds.NewFolderInFolder)]
    internal sealed class CreateSubfolderCommand : BaseCommand<CreateSubfolderCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var currentItem = FavoritesContextMenuController.CurrentItem;

            if (currentItem is FavoriteFolderNode folderNode)
            {
                var folderName = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter folder name:",
                    "New Folder",
                    "New Folder");

                if (!string.IsNullOrWhiteSpace(folderName))
                {
                    FavoritesManager.Instance.CreateFolderIn(folderName.Trim(), folderNode.Item);
                }
            }
        }
    }
}
