using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Internal.VisualStudio.PlatformUI;

namespace SolutionFavorites.MEF
{
    /// <summary>
    /// Handles drag operations for favorite items (files and folders).
    /// </summary>
    internal sealed class FavoritesDragDropController : IDragDropSourceController
    {
        private static FavoritesDragDropController _instance;

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static FavoritesDragDropController Instance =>
            _instance ??= new FavoritesDragDropController();

        private FavoritesDragDropController() { }

        /// <summary>
        /// Initiates a drag operation for the selected items.
        /// </summary>
        public bool DoDragDrop(IEnumerable<object> items)
        {
            var dragItems = items.Where(i => i is FavoriteFileNode or FavoriteFolderNode).ToList();
            if (!dragItems.Any())
            {
                return false;
            }

            DependencyObject dragSource = (Keyboard.FocusedElement as DependencyObject) ?? Application.Current.MainWindow;

            // Store the actual node objects for drag-drop
            var dataObj = new DataObject(DragDropFormats.Favorites, dragItems.ToArray());

            _ = DragDrop.DoDragDrop(dragSource, dataObj, DragDropEffects.Move);

            return true;
        }
    }
}
