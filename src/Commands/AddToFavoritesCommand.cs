using EnvDTE;

namespace SolutionFavorites.Commands
{
    /// <summary>
    /// Command to add the selected file to favorites.
    /// </summary>
    [Command(PackageIds.AddToFavorites)]
    internal sealed class AddToFavoritesCommand : BaseCommand<AddToFavoritesCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = await VS.GetServiceAsync<DTE, DTE>();
            if (dte?.SelectedItems == null)
                return;

            foreach (SelectedItem selectedItem in dte.SelectedItems)
            {
                var projectItem = selectedItem.ProjectItem;
                if (projectItem != null)
                {
                    var filePath = projectItem.FileNames[1];
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        FavoritesManager.Instance.AddFile(filePath);
                    }
                }
            }
        }
    }
}
