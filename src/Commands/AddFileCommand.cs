using SolutionFavorites.Helpers;

namespace SolutionFavorites.Commands
{
    /// <summary>
    /// Command to add a file from disk to the Favorites root.
    /// </summary>
    [Command(PackageIds.AddFile)]
    internal sealed class AddFileCommand : BaseCommand<AddFileCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            foreach (var filePath in FileDialogHelper.BrowseForFiles())
            {
                FavoritesManager.Instance.AddFile(filePath);
            }
        }
    }
}
