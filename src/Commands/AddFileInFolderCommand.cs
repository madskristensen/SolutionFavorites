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
                using (var dialog = new System.Windows.Forms.OpenFileDialog())
                {
                    dialog.Title = "Add File to Favorites";
                    dialog.Filter = "All files (*.*)|*.*";
                    dialog.Multiselect = true;

                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        foreach (var fileName in dialog.FileNames)
                        {
                            FavoritesManager.Instance.AddFileToFolder(fileName, folderNode.Item);
                        }
                    }
                }
            }
        }
    }
}
