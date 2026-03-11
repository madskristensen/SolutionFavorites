using System.IO;
using System.Reflection;
using EnvDTE;

namespace SolutionFavorites.Commands
{
    /// <summary>
    /// Command to add the selected item to favorites.
    /// Works with both Solution Explorer project items and WorkspaceFiles (File Explorer) nodes.
    /// </summary>
    [Command(PackageIds.AddToFavorites)]
    internal sealed class AddToFavoritesCommand : BaseCommand<AddToFavoritesCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // First, try to handle a WorkspaceFiles (File Explorer) node via reflection,
            // avoiding a hard compile-time dependency on that assembly.
            if (TryHandleWorkspaceFilesNode())
            {
                return;
            }

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
                    var filePath = projectItem.FileNames[1];
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        _ = FavoritesManager.Instance.AddFile(filePath);
                    }
                }
            }
        }

        /// <summary>
        /// Tries to handle the currently selected WorkspaceFiles (File Explorer) node.
        /// For file nodes, adds the file directly. For folder nodes, recursively adds all contents.
        /// Returns true if a WorkspaceFiles node was found and handled.
        /// </summary>
        private static bool TryHandleWorkspaceFilesNode()
        {
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!assembly.FullName.StartsWith("WorkspaceFiles,", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var controllerType = assembly.GetType("WorkspaceFiles.WorkspaceItemContextMenuController");
                    if (controllerType == null)
                    {
                        break;
                    }

                    var currentItemProp = controllerType.GetProperty("CurrentItem", BindingFlags.Public | BindingFlags.Static);
                    var currentItem = currentItemProp?.GetValue(null);
                    if (currentItem == null)
                    {
                        break;
                    }

                    var itemType = currentItem.GetType().GetProperty("Type")?.GetValue(currentItem);
                    if (itemType == null)
                    {
                        break;
                    }

                    var info = currentItem.GetType().GetProperty("Info")?.GetValue(currentItem);
                    var fullPath = info?.GetType().GetProperty("FullName")?.GetValue(info) as string;
                    if (string.IsNullOrEmpty(fullPath))
                    {
                        break;
                    }

                    var typeValue = (int)itemType;

                    // WorkspaceItemType.File == 0
                    if (typeValue == 0)
                    {
                        FavoritesManager.Instance.AddFile(fullPath);
                        return true;
                    }

                    // WorkspaceItemType.Folder == 1
                    if (typeValue == 1)
                    {
                        var folderName = Path.GetFileName(fullPath);
                        var favoritesFolder = FavoritesManager.Instance.CreateFolder(folderName);
                        if (favoritesFolder != null)
                        {
                            AddFolderFromDiskCommand.AddFilesFromDirectory(fullPath, favoritesFolder);
                        }
                        return true;
                    }

                    break;
                }
            }
            catch (Exception)
            {
                // If reflection fails for any reason, fall through to DTE path
            }

            return false;
        }
    }
}
