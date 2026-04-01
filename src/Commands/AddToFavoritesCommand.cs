using System.IO;
using System.Reflection;
using EnvDTE;

namespace SolutionFavorites.Commands
{
    /// <summary>
    /// Command to add the selected item to favorites.
    /// Works with document tabs, Solution Explorer project items, and WorkspaceFiles (File Explorer) nodes.
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

            // Next, try to handle Solution Explorer selection via DTE
            DTE dte = await VS.GetServiceAsync<DTE, DTE>();
            if (dte?.SelectedItems != null && dte.SelectedItems.Count > 0)
            {
                bool handledAny = false;
                foreach (SelectedItem selectedItem in dte.SelectedItems)
                {
                    ProjectItem projectItem = selectedItem.ProjectItem;
                    if (projectItem != null)
                    {
                        var filePath = projectItem.FileNames[1];
                        if (!string.IsNullOrEmpty(filePath))
                        {
                            _ = FavoritesManager.Instance.AddFile(filePath);
                            handledAny = true;
                        }
                    }
                }

                if (handledAny)
                {
                    return;
                }
            }

            // Finally, try to get the active document (for document tab context menu)
            // This is used when right-clicking on a document tab where there's no selection
            await TryHandleActiveDocumentAsync();
        }

        /// <summary>
        /// Tries to get the active document and add it to favorites.
        /// This is used as a fallback when there's no explicit selection (e.g., document tab context menu).
        /// Returns true if an active document was found and added.
        /// </summary>
        private static async System.Threading.Tasks.Task<bool> TryHandleActiveDocumentAsync()
        {
            try
            {
                var docView = await VS.Documents.GetActiveDocumentViewAsync();
                if (docView?.FilePath != null && File.Exists(docView.FilePath))
                {
                    _ = FavoritesManager.Instance.AddFile(docView.FilePath);
                    return true;
                }
            }
            catch (Exception)
            {
                // If getting active document fails, fall through to other methods
            }

            return false;
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
