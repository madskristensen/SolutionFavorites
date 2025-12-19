using Microsoft.Internal.VisualStudio.PlatformUI;

namespace SolutionFavorites.Commands
{
    /// <summary>
    /// Command to toggle visibility of the Favorites node in Solution Explorer.
    /// </summary>
    [Command(PackageIds.ToggleFavoritesVisibility)]
    internal sealed class ToggleFavoritesVisibilityCommand : BaseCommand<ToggleFavoritesVisibilityCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Visible = HierarchyUtilities.IsSolutionOpen;
            Command.Checked = FavoritesManager.Instance.IsVisible;
        }

        protected override Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            FavoritesManager.Instance.IsVisible = !FavoritesManager.Instance.IsVisible;
            return Task.CompletedTask;
        }
    }
}
