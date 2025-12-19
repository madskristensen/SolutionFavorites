namespace SolutionFavorites.Helpers
{
    /// <summary>
    /// Helper methods for file dialogs.
    /// </summary>
    internal static class FileDialogHelper
    {
        /// <summary>
        /// Opens a file browser dialog and returns the selected file paths.
        /// </summary>
        /// <param name="title">Dialog title.</param>
        /// <returns>Array of selected file paths, or empty array if cancelled.</returns>
        public static string[] BrowseForFiles(string title = "Add File to Favorites")
        {
            using (var dialog = new System.Windows.Forms.OpenFileDialog())
            {
                dialog.Title = title;
                dialog.Filter = "All files (*.*)|*.*";
                dialog.Multiselect = true;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    return dialog.FileNames;
                }

                return [];
            }
        }
    }
}
