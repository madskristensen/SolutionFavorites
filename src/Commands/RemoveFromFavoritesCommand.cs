using SolutionFavorites.MEF;

namespace SolutionFavorites.Commands
{
    /// <summary>
    /// Command to remove a file from favorites.
    /// </summary>
    [Command(PackageIds.RemoveFromFavorites)]
    internal sealed class RemoveFromFavoritesCommand : BaseCommand<RemoveFromFavoritesCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var currentItem = FavoritesContextMenuController.CurrentItem;

            if (currentItem is FavoriteFileNode fileNode)
            {
                FavoritesManager.Instance.Remove(fileNode.Item.Id);
            }
        }
    }
}
