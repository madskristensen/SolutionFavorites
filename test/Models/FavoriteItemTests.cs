using SolutionFavorites.Models;

namespace SolutionFavorites.Test.Models
{
    [TestClass]
    public sealed class FavoriteItemTests
    {
        [TestMethod]
        public void CreateFile_SetsPathAndDerivesName()
        {
            var item = FavoriteItem.CreateFile(@"src\Helpers\FileDialogHelper.cs");

            Assert.AreEqual(@"src\Helpers\FileDialogHelper.cs", item.Path);
            Assert.AreEqual("FileDialogHelper.cs", item.Name);
            Assert.IsFalse(item.IsFolder);
            Assert.IsNull(item.Children);
        }

        [TestMethod]
        public void CreateFile_UsesExplicitNameWhenProvided()
        {
            var item = FavoriteItem.CreateFile(@"src\Foo.cs", "My File");

            Assert.AreEqual("My File", item.Name);
            Assert.AreEqual(@"src\Foo.cs", item.Path);
        }

        [TestMethod]
        public void CreateFolder_SetsNameAndInitializesChildren()
        {
            var folder = FavoriteItem.CreateFolder("MyFolder");

            Assert.AreEqual("MyFolder", folder.Name);
            Assert.IsTrue(folder.IsFolder);
            Assert.IsNotNull(folder.Children);
            Assert.AreEqual(0, folder.Children.Count);
            Assert.IsNull(folder.Path);
        }

        [TestMethod]
        public void IsFolder_ReturnsFalseWhenChildrenIsNull()
        {
            var item = new FavoriteItem { Name = "file.cs", Path = @"src\file.cs" };

            Assert.IsFalse(item.IsFolder);
        }

        [TestMethod]
        public void IsFolder_ReturnsTrueWhenChildrenIsNotNull()
        {
            var item = new FavoriteItem { Name = "Folder", Children = [] };

            Assert.IsTrue(item.IsFolder);
        }

        [TestMethod]
        public void FavoritesData_DefaultsToVersion2WithEmptyItems()
        {
            var data = new FavoritesData();

            Assert.AreEqual(2, data.Version);
            Assert.IsNotNull(data.Items);
            Assert.AreEqual(0, data.Items.Count);
        }
    }
}
