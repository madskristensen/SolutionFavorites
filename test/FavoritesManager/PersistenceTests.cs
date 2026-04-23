using System.IO;
using Newtonsoft.Json;
using SolutionFavorites.Models;

namespace SolutionFavorites.Test.FavoritesManager
{
    [TestClass]
    public sealed class PersistenceTests
    {
        // Each test gets its own temp dir — no cleanup needed, random paths never collide
        private static string CreateSolutionPath()
        {
            var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "Test.sln");
        }

        private static string FavoritesFilePath(string solutionPath)
            => Path.Combine(Path.GetDirectoryName(solutionPath)!, "favorites.json");

        private static void WriteFavoritesJson(string solutionPath, FavoritesData data)
            => File.WriteAllText(FavoritesFilePath(solutionPath), JsonConvert.SerializeObject(data, Formatting.Indented));

        // --- LoadForSolution: no file ---

        [TestMethod]
        public void LoadForSolution_NoFile_StartsEmpty()
        {
            var solutionPath = CreateSolutionPath();
            var manager = SolutionFavorites.FavoritesManager.CreateForTesting();

            manager.LoadForSolution(solutionPath);

            Assert.IsFalse(manager.HasFavorites);
            Assert.IsFalse(manager.IsVisible);
        }

        [TestMethod]
        public void LoadForSolution_NoFile_SetsSolutionDirectory()
        {
            var solutionPath = CreateSolutionPath();
            var manager = SolutionFavorites.FavoritesManager.CreateForTesting();

            manager.LoadForSolution(solutionPath);

            Assert.AreEqual(Path.GetDirectoryName(solutionPath), manager.SolutionDirectory);
        }

        // --- LoadForSolution: valid file ---

        [TestMethod]
        public void LoadForSolution_ValidFile_LoadsItems()
        {
            var solutionPath = CreateSolutionPath();
            var data = new FavoritesData();
            data.Items.Add(FavoriteItem.CreateFile(@"src\Program.cs"));
            data.Items.Add(FavoriteItem.CreateFile(@"src\App.cs"));
            WriteFavoritesJson(solutionPath, data);

            var manager = SolutionFavorites.FavoritesManager.CreateForTesting();
            manager.LoadForSolution(solutionPath);

            Assert.IsTrue(manager.HasFavorites);
        }

        [TestMethod]
        public void LoadForSolution_ValidFile_SortsItemsOnLoad()
        {
            var solutionPath = CreateSolutionPath();
            var data = new FavoritesData();
            data.Items.Add(FavoriteItem.CreateFile(@"src\Z.cs", "Z.cs"));
            data.Items.Add(FavoriteItem.CreateFolder("Alpha"));
            data.Items.Add(FavoriteItem.CreateFile(@"src\A.cs", "A.cs"));
            WriteFavoritesJson(solutionPath, data);

            var manager = SolutionFavorites.FavoritesManager.CreateForTesting();

            // Should not throw — sorting happens inside LoadForSolution without VS dependencies
            manager.LoadForSolution(solutionPath);

            Assert.IsTrue(manager.HasFavorites);
        }

        [TestMethod]
        public void LoadForSolution_ValidFile_BuildsPathIndexForDuplicateDetection()
        {
            var solutionPath = CreateSolutionPath();
            var data = new FavoritesData();
            data.Items.Add(FavoriteItem.CreateFile(@"src\Program.cs"));
            WriteFavoritesJson(solutionPath, data);

            var manager = SolutionFavorites.FavoritesManager.CreateForTesting();
            manager.LoadForSolution(solutionPath);

            // IsFileFavorited uses the path index — absolute path converted to relative internally
            var absolutePath = Path.Combine(manager.SolutionDirectory!, @"src\Program.cs");
            Assert.IsTrue(manager.IsFileFavorited(absolutePath));
        }

        [TestMethod]
        public void LoadForSolution_WithDuplicates_IndexesAllOccurrences()
        {
            var solutionPath = CreateSolutionPath();
            var data = new FavoritesData();
            data.Items.Add(FavoriteItem.CreateFile(@"src\Program.cs"));
            data.Items.Add(FavoriteItem.CreateFile(@"src\Program.cs"));
            WriteFavoritesJson(solutionPath, data);

            var manager = SolutionFavorites.FavoritesManager.CreateForTesting();
            manager.LoadForSolution(solutionPath);

            var absolutePath = Path.Combine(manager.SolutionDirectory!, @"src\Program.cs");
            Assert.IsTrue(manager.HasFavorites);
            Assert.IsTrue(manager.IsFileFavorited(absolutePath));
        }

        [TestMethod]
        public void LoadForSolution_ValidFile_IsVisibleWhenItemsPresent()
        {
            var solutionPath = CreateSolutionPath();
            var data = new FavoritesData();
            data.Items.Add(FavoriteItem.CreateFile(@"src\Program.cs"));
            WriteFavoritesJson(solutionPath, data);

            var manager = SolutionFavorites.FavoritesManager.CreateForTesting();
            manager.LoadForSolution(solutionPath);

            Assert.IsTrue(manager.IsVisible);
        }

        // --- LoadForSolution: corrupt file ---

        [TestMethod]
        public void LoadForSolution_CorruptJson_FallsBackToEmpty()
        {
            var solutionPath = CreateSolutionPath();
            File.WriteAllText(FavoritesFilePath(solutionPath), "{ this is not valid json ]]]");

            var manager = SolutionFavorites.FavoritesManager.CreateForTesting();
            manager.LoadForSolution(solutionPath);

            Assert.IsFalse(manager.HasFavorites);
            Assert.IsFalse(manager.IsVisible);
        }

        [TestMethod]
        public void LoadForSolution_EmptyJson_FallsBackToEmpty()
        {
            var solutionPath = CreateSolutionPath();
            File.WriteAllText(FavoritesFilePath(solutionPath), string.Empty);

            var manager = SolutionFavorites.FavoritesManager.CreateForTesting();
            manager.LoadForSolution(solutionPath);

            Assert.IsFalse(manager.HasFavorites);
        }

        // --- LoadForSolution: reloading ---

        [TestMethod]
        public void LoadForSolution_ReloadClearsExistingState()
        {
            var solutionPath1 = CreateSolutionPath();
            var data = new FavoritesData();
            data.Items.Add(FavoriteItem.CreateFile(@"src\Program.cs"));
            WriteFavoritesJson(solutionPath1, data);

            var manager = SolutionFavorites.FavoritesManager.CreateForTesting();
            manager.LoadForSolution(solutionPath1);
            Assert.IsTrue(manager.HasFavorites);

            // Load a different solution with no favorites file
            var solutionPath2 = CreateSolutionPath();
            manager.LoadForSolution(solutionPath2);

            Assert.IsFalse(manager.HasFavorites);
            Assert.AreEqual(Path.GetDirectoryName(solutionPath2), manager.SolutionDirectory);
        }

        // --- Clear ---

        [TestMethod]
        public void Clear_ResetsAllState()
        {
            var solutionPath = CreateSolutionPath();
            var data = new FavoritesData();
            data.Items.Add(FavoriteItem.CreateFile(@"src\Program.cs"));
            WriteFavoritesJson(solutionPath, data);

            var manager = SolutionFavorites.FavoritesManager.CreateForTesting();
            manager.LoadForSolution(solutionPath);
            Assert.IsTrue(manager.HasFavorites);

            manager.Clear();

            Assert.IsFalse(manager.HasFavorites);
            Assert.IsNull(manager.SolutionDirectory);
        }
    }
}
