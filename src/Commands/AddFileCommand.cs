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

            using (var dialog = new System.Windows.Forms.OpenFileDialog())
            {
                dialog.Title = "Add File to Favorites";
                dialog.Filter = "All files (*.*)|*.*";
                dialog.Multiselect = true;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    foreach (var fileName in dialog.FileNames)
                    {
                        FavoritesManager.Instance.AddFile(fileName);
                    }
                }
            }
        }
    }
}
