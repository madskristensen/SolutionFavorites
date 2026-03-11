using System.IO;
using SolutionFavorites.Helpers;
using SolutionFavorites.Models;

namespace SolutionFavorites.Commands
{
    /// <summary>
    /// Command to add a folder from disk to favorites, recursively adding all its files.
    /// </summary>
    [Command(PackageIds.AddFolderFromDisk)]
    internal sealed class AddFolderFromDiskCommand : BaseCommand<AddFolderFromDiskCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var folderPath = FileDialogHelper.BrowseForFolder();
            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            var folderName = Path.GetFileName(folderPath);
            FavoriteItem favoritesFolder = FavoritesManager.Instance.CreateFolder(folderName);
            if (favoritesFolder == null)
            {
                return;
            }

            AddFilesFromDirectory(folderPath, favoritesFolder);
        }

        /// <summary>
        /// Recursively adds all files from a disk directory into a favorites folder.
        /// </summary>
        internal static void AddFilesFromDirectory(string directoryPath, FavoriteItem targetFolder)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                foreach (var filePath in Directory.GetFiles(directoryPath))
                {
                    FavoritesManager.Instance.AddFileToFolder(filePath, targetFolder);
                }

                foreach (var subDirectory in Directory.GetDirectories(directoryPath))
                {
                    var subFolderName = Path.GetFileName(subDirectory);
                    FavoriteItem subFolder = FavoritesManager.Instance.CreateFolderIn(subFolderName, targetFolder);
                    if (subFolder != null)
                    {
                        AddFilesFromDirectory(subDirectory, subFolder);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we don't have permission to read
            }
        }
    }
}
