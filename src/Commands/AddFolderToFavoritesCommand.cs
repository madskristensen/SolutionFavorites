using System.IO;
using EnvDTE;
using SolutionFavorites.Models;

namespace SolutionFavorites.Commands
{
    /// <summary>
    /// Command to add a folder and its files to favorites.
    /// </summary>
    [Command(PackageIds.AddFolderToFavorites)]
    internal sealed class AddFolderToFavoritesCommand : BaseCommand<AddFolderToFavoritesCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            DTE dte = await VS.GetServiceAsync<DTE, DTE>();
            if (dte?.SelectedItems == null)
            {
                return;
            }

            foreach (SelectedItem selectedItem in dte.SelectedItems)
            {
                ProjectItem projectItem = selectedItem.ProjectItem;
                if (projectItem != null)
                {
                    AddProjectItemFolder(projectItem);
                }
            }
        }

        /// <summary>
        /// Adds a project folder and all its files to favorites.
        /// </summary>
        private void AddProjectItemFolder(ProjectItem projectItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Get folder name and create a favorites folder with the same name
            var folderName = projectItem.Name;
            if (string.IsNullOrEmpty(folderName))
            {
                return;
            }

            FavoriteItem favoritesFolder = FavoritesManager.Instance.CreateFolder(folderName);
            if (favoritesFolder == null)
            {
                return;
            }

            // Add all files from the project folder
            AddFilesFromProjectItem(projectItem, favoritesFolder);
        }

        /// <summary>
        /// Recursively adds files from a project item to a favorites folder.
        /// </summary>
        private void AddFilesFromProjectItem(ProjectItem projectItem, Models.FavoriteItem targetFolder)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (projectItem.ProjectItems == null)
            {
                return;
            }

            foreach (ProjectItem childItem in projectItem.ProjectItems)
            {
                // Check if it's a file (has a file path)
                string filePath = null;
                try
                {
                    filePath = childItem.FileNames[1];
                }
                catch
                {
                    // Some items don't have file paths
                }

                if (!string.IsNullOrEmpty(filePath))
                {
                    if (Directory.Exists(filePath))
                    {
                        // It's a subfolder - create nested folder and recurse
                        FavoriteItem subFolder = FavoritesManager.Instance.CreateFolderIn(childItem.Name, targetFolder);
                        if (subFolder != null)
                        {
                            AddFilesFromProjectItem(childItem, subFolder);
                        }
                    }
                    else if (File.Exists(filePath))
                    {
                        // It's a file - add it to the target folder
                        _ = FavoritesManager.Instance.AddFileToFolder(filePath, targetFolder);
                    }
                }
            }
        }
    }
}
