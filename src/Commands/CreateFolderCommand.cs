namespace SolutionFavorites.Commands
{
    /// <summary>
    /// Command to create a new folder in the Favorites root.
    /// </summary>
    [Command(PackageIds.NewFolder)]
    internal sealed class CreateFolderCommand : BaseCommand<CreateFolderCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var folderName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter folder name:",
                "New Folder",
                "New Folder");

            if (!string.IsNullOrWhiteSpace(folderName))
            {
                FavoritesManager.Instance.CreateFolder(folderName.Trim());
            }
        }
    }
}
