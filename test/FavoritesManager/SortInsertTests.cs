using System.Collections.Generic;
using SolutionFavorites.Models;

namespace SolutionFavorites.Test.FavoritesManager
{
    [TestClass]
    public sealed class SortInsertTests
    {
        // --- InsertSorted ---

        [TestMethod]
        public void InsertSorted_InsertsFileIntoEmptyList()
        {
            var items = new List<FavoriteItem>();
            var file = FavoriteItem.CreateFile(@"src\A.cs");

            SolutionFavorites.FavoritesManager.InsertSorted(items, file);

            Assert.AreEqual(1, items.Count);
            Assert.AreSame(file, items[0]);
        }

        [TestMethod]
        public void InsertSorted_FolderComesBeforeFile()
        {
            var items = new List<FavoriteItem>();
            var file = FavoriteItem.CreateFile(@"src\A.cs");
            var folder = FavoriteItem.CreateFolder("Folder");

            SolutionFavorites.FavoritesManager.InsertSorted(items, file);
            SolutionFavorites.FavoritesManager.InsertSorted(items, folder);

            Assert.IsTrue(items[0].IsFolder);
            Assert.IsFalse(items[1].IsFolder);
        }

        [TestMethod]
        public void InsertSorted_FilesAreSortedAlphabetically()
        {
            var items = new List<FavoriteItem>();

            SolutionFavorites.FavoritesManager.InsertSorted(items, FavoriteItem.CreateFile(@"src\Z.cs", "Z.cs"));
            SolutionFavorites.FavoritesManager.InsertSorted(items, FavoriteItem.CreateFile(@"src\A.cs", "A.cs"));
            SolutionFavorites.FavoritesManager.InsertSorted(items, FavoriteItem.CreateFile(@"src\M.cs", "M.cs"));

            Assert.AreEqual("A.cs", items[0].Name);
            Assert.AreEqual("M.cs", items[1].Name);
            Assert.AreEqual("Z.cs", items[2].Name);
        }

        [TestMethod]
        public void InsertSorted_FoldersAreSortedAlphabetically()
        {
            var items = new List<FavoriteItem>();

            SolutionFavorites.FavoritesManager.InsertSorted(items, FavoriteItem.CreateFolder("Zebra"));
            SolutionFavorites.FavoritesManager.InsertSorted(items, FavoriteItem.CreateFolder("Alpha"));
            SolutionFavorites.FavoritesManager.InsertSorted(items, FavoriteItem.CreateFolder("Mango"));

            Assert.AreEqual("Alpha", items[0].Name);
            Assert.AreEqual("Mango", items[1].Name);
            Assert.AreEqual("Zebra", items[2].Name);
        }

        [TestMethod]
        public void InsertSorted_MixedListHasFoldersBeforeFilesEachAlphabetical()
        {
            var items = new List<FavoriteItem>();

            SolutionFavorites.FavoritesManager.InsertSorted(items, FavoriteItem.CreateFile(@"src\Z.cs", "Z.cs"));
            SolutionFavorites.FavoritesManager.InsertSorted(items, FavoriteItem.CreateFolder("Beta"));
            SolutionFavorites.FavoritesManager.InsertSorted(items, FavoriteItem.CreateFile(@"src\A.cs", "A.cs"));
            SolutionFavorites.FavoritesManager.InsertSorted(items, FavoriteItem.CreateFolder("Alpha"));

            Assert.AreEqual("Alpha", items[0].Name);
            Assert.AreEqual("Beta", items[1].Name);
            Assert.AreEqual("A.cs", items[2].Name);
            Assert.AreEqual("Z.cs", items[3].Name);
        }

        [TestMethod]
        public void InsertSorted_IsCaseInsensitive()
        {
            var items = new List<FavoriteItem>();

            SolutionFavorites.FavoritesManager.InsertSorted(items, FavoriteItem.CreateFile(@"src\b.cs", "b.cs"));
            SolutionFavorites.FavoritesManager.InsertSorted(items, FavoriteItem.CreateFile(@"src\A.cs", "A.cs"));

            Assert.AreEqual("A.cs", items[0].Name);
            Assert.AreEqual("b.cs", items[1].Name);
        }

        // --- SortItemsInPlace ---

        [TestMethod]
        public void SortItemsInPlace_OrdersFoldersBeforeFiles()
        {
            var items = new List<FavoriteItem>
            {
                FavoriteItem.CreateFile(@"src\A.cs", "A.cs"),
                FavoriteItem.CreateFolder("FolderB"),
                FavoriteItem.CreateFile(@"src\B.cs", "B.cs"),
                FavoriteItem.CreateFolder("FolderA"),
            };

            SolutionFavorites.FavoritesManager.SortItemsInPlace(items);

            Assert.IsTrue(items[0].IsFolder);
            Assert.IsTrue(items[1].IsFolder);
            Assert.IsFalse(items[2].IsFolder);
            Assert.IsFalse(items[3].IsFolder);
            Assert.AreEqual("FolderA", items[0].Name);
            Assert.AreEqual("FolderB", items[1].Name);
        }

        [TestMethod]
        public void SortItemsInPlace_RecursivelySortsChildItems()
        {
            var folder = FavoriteItem.CreateFolder("Parent");
            folder.Children!.Add(FavoriteItem.CreateFile(@"src\Z.cs", "Z.cs"));
            folder.Children.Add(FavoriteItem.CreateFile(@"src\A.cs", "A.cs"));

            var items = new List<FavoriteItem> { folder };

            SolutionFavorites.FavoritesManager.SortItemsInPlace(items);

            Assert.AreEqual("A.cs", folder.Children[0].Name);
            Assert.AreEqual("Z.cs", folder.Children[1].Name);
        }
    }
}
