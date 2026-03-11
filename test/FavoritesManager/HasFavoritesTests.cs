using System.IO;
using SolutionFavorites.Models;

namespace SolutionFavorites.Test.FavoritesManager
{
    [TestClass]
    public sealed class HasFavoritesTests
    {
        private static SolutionFavorites.FavoritesManager CreateLoaded()
        {
            var solutionPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "Test.sln");
            Directory.CreateDirectory(Path.GetDirectoryName(solutionPath)!);
            var manager = SolutionFavorites.FavoritesManager.CreateForTesting();
            manager.LoadForSolution(solutionPath);
            return manager;
        }

        // --- HasFavorites ---

        [TestMethod]
        public void HasFavorites_ReturnsFalseWhenEmpty()
        {
            var manager = CreateLoaded();

            Assert.IsFalse(manager.HasFavorites);
        }

        [TestMethod]
        public void HasFavorites_ReturnsTrueWhenItemsLoadedFromJson()
        {
            var solutionPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "Test.sln");
            Directory.CreateDirectory(Path.GetDirectoryName(solutionPath)!);
            var favoritesPath = Path.Combine(Path.GetDirectoryName(solutionPath)!, "favorites.json");

            var data = new FavoritesData();
            data.Items.Add(FavoriteItem.CreateFile(@"src\Program.cs"));
            File.WriteAllText(favoritesPath, Newtonsoft.Json.JsonConvert.SerializeObject(data));

            var manager = SolutionFavorites.FavoritesManager.CreateForTesting();
            manager.LoadForSolution(solutionPath);

            Assert.IsTrue(manager.HasFavorites);
        }

        // --- IsFileFavorited ---

        [TestMethod]
        public void IsFileFavorited_ReturnsFalseForUnknownFile()
        {
            var manager = CreateLoaded();

            Assert.IsFalse(manager.IsFileFavorited(@"C:\anything\file.cs"));
        }

        [TestMethod]
        public void IsFileFavorited_ReturnsTrueAfterLoadingJsonWithThatFile()
        {
            var solutionPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "Test.sln");
            Directory.CreateDirectory(Path.GetDirectoryName(solutionPath)!);
            var favoritesPath = Path.Combine(Path.GetDirectoryName(solutionPath)!, "favorites.json");

            var data = new FavoritesData();
            data.Items.Add(FavoriteItem.CreateFile(@"src\Program.cs"));
            File.WriteAllText(favoritesPath, Newtonsoft.Json.JsonConvert.SerializeObject(data));

            var manager = SolutionFavorites.FavoritesManager.CreateForTesting();
            manager.LoadForSolution(solutionPath);

            var absolutePath = Path.Combine(manager.SolutionDirectory!, @"src\Program.cs");
            Assert.IsTrue(manager.IsFileFavorited(absolutePath));
        }

        [TestMethod]
        public void IsFileFavorited_IsCaseInsensitive()
        {
            var solutionPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "Test.sln");
            Directory.CreateDirectory(Path.GetDirectoryName(solutionPath)!);
            var favoritesPath = Path.Combine(Path.GetDirectoryName(solutionPath)!, "favorites.json");

            var data = new FavoritesData();
            data.Items.Add(FavoriteItem.CreateFile(@"src\Program.cs"));
            File.WriteAllText(favoritesPath, Newtonsoft.Json.JsonConvert.SerializeObject(data));

            var manager = SolutionFavorites.FavoritesManager.CreateForTesting();
            manager.LoadForSolution(solutionPath);

            var upperPath = Path.Combine(manager.SolutionDirectory!, @"SRC\PROGRAM.CS");
            Assert.IsTrue(manager.IsFileFavorited(upperPath));
        }

        [TestMethod]
        public void IsFileFavorited_ReturnsFalseAfterClear()
        {
            var solutionPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "Test.sln");
            Directory.CreateDirectory(Path.GetDirectoryName(solutionPath)!);
            var favoritesPath = Path.Combine(Path.GetDirectoryName(solutionPath)!, "favorites.json");

            var data = new FavoritesData();
            data.Items.Add(FavoriteItem.CreateFile(@"src\Program.cs"));
            File.WriteAllText(favoritesPath, Newtonsoft.Json.JsonConvert.SerializeObject(data));

            var manager = SolutionFavorites.FavoritesManager.CreateForTesting();
            manager.LoadForSolution(solutionPath);
            var absolutePath = Path.Combine(manager.SolutionDirectory!, @"src\Program.cs");
            Assert.IsTrue(manager.IsFileFavorited(absolutePath));

            manager.Clear();

            Assert.IsFalse(manager.IsFileFavorited(absolutePath));
        }
    }
}
