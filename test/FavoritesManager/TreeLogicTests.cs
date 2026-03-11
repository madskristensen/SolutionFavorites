using SolutionFavorites.Models;

namespace SolutionFavorites.Test.FavoritesManager
{
    [TestClass]
    public sealed class TreeLogicTests
    {
        private SolutionFavorites.FavoritesManager _manager = null!;

        [TestInitialize]
        public void Initialize()
        {
            var solutionPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TestSolution", "TestSolution.sln");
            _manager = SolutionFavorites.FavoritesManager.CreateForTesting();
            _manager.LoadForSolution(solutionPath);
        }

        // --- IsDescendantOf ---

        [TestMethod]
        public void IsDescendantOf_ReturnsFalseForAncestorWithNoChildren()
        {
            var ancestor = new FavoriteItem { Name = "Ancestor" }; // no children = not a folder
            var potential = FavoriteItem.CreateFolder("Child");

            var result = _manager.IsDescendantOf(potential, ancestor);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsDescendantOf_ReturnsTrueForDirectChild()
        {
            var ancestor = FavoriteItem.CreateFolder("Parent");
            var child = FavoriteItem.CreateFolder("Child");
            ancestor.Children!.Add(child);

            var result = _manager.IsDescendantOf(child, ancestor);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsDescendantOf_ReturnsTrueForDeepNestedDescendant()
        {
            var root = FavoriteItem.CreateFolder("Root");
            var middle = FavoriteItem.CreateFolder("Middle");
            var deep = FavoriteItem.CreateFolder("Deep");
            middle.Children!.Add(deep);
            root.Children!.Add(middle);

            var result = _manager.IsDescendantOf(deep, root);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsDescendantOf_ReturnsFalseForUnrelatedItem()
        {
            var ancestor = FavoriteItem.CreateFolder("Parent");
            ancestor.Children!.Add(FavoriteItem.CreateFolder("Child"));
            var unrelated = FavoriteItem.CreateFolder("Unrelated");

            var result = _manager.IsDescendantOf(unrelated, ancestor);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsDescendantOf_ReturnsFalseWhenAncestorIsNull()
        {
            var potential = FavoriteItem.CreateFolder("Child");

            var result = _manager.IsDescendantOf(potential, null!);

            Assert.IsFalse(result);
        }

        // --- GetFolderItems ---

        [TestMethod]
        public void GetFolderItems_ReturnsEmptyForNullFolder()
        {
            var result = _manager.GetFolderItems(null!);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void GetFolderItems_ReturnsEmptyForFolderWithNullChildren()
        {
            var item = new FavoriteItem { Name = "NotAFolder" }; // Children is null

            var result = _manager.GetFolderItems(item);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void GetFolderItems_ReturnsChildrenOfFolder()
        {
            var folder = FavoriteItem.CreateFolder("MyFolder");
            folder.Children!.Add(FavoriteItem.CreateFile(@"src\A.cs", "A.cs"));
            folder.Children.Add(FavoriteItem.CreateFile(@"src\B.cs", "B.cs"));

            var result = _manager.GetFolderItems(folder);

            Assert.AreEqual(2, result.Count);
        }
    }
}
