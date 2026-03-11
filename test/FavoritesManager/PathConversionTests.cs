using System.IO;
using SolutionFavorites.Models;

namespace SolutionFavorites.Test.FavoritesManager
{
    [TestClass]
    public sealed class PathConversionTests
    {
        private SolutionFavorites.FavoritesManager _manager = null!;

        [TestInitialize]
        public void Initialize()
        {
            // Use a temp path as the solution root so no DTE/VS dependency is needed
            var solutionPath = Path.Combine(Path.GetTempPath(), "TestSolution", "TestSolution.sln");
            _manager = SolutionFavorites.FavoritesManager.CreateForTesting();
            _manager.LoadForSolution(solutionPath);
        }

        [TestMethod]
        public void ToRelativePath_ReturnsRelativePathForFileInsideSolution()
        {
            var solutionDir = _manager.SolutionDirectory!;
            var absolute = Path.Combine(solutionDir, "src", "Program.cs");

            var result = _manager.ToRelativePath(absolute);

            Assert.AreEqual(@"src\Program.cs", result);
        }

        [TestMethod]
        public void ToRelativePath_ReturnsOriginalPathForFileOutsideSolution()
        {
            var outside = @"C:\OtherProject\file.cs";

            var result = _manager.ToRelativePath(outside);

            Assert.AreEqual(outside, result);
        }

        [TestMethod]
        public void ToRelativePath_ReturnsNullForNullInput()
        {
            var result = _manager.ToRelativePath(null!);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ToRelativePath_ReturnsEmptyStringForEmptyInput()
        {
            var result = _manager.ToRelativePath(string.Empty);

            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void ToAbsolutePath_CombinesSolutionDirectoryWithRelativePath()
        {
            var solutionDir = _manager.SolutionDirectory!;

            var result = _manager.ToAbsolutePath(@"src\Program.cs");

            Assert.AreEqual(Path.GetFullPath(Path.Combine(solutionDir, @"src\Program.cs")), result);
        }

        [TestMethod]
        public void ToAbsolutePath_ReturnsRootedPathUnchanged()
        {
            var absolute = @"C:\SomeOtherDir\file.cs";

            var result = _manager.ToAbsolutePath(absolute);

            Assert.AreEqual(absolute, result);
        }

        [TestMethod]
        public void ToAbsolutePath_ReturnsNullWhenInputIsNull()
        {
            var result = _manager.ToAbsolutePath(null!);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void RoundTrip_RelativeAndAbsolutePathsAreSymmetric()
        {
            var solutionDir = _manager.SolutionDirectory!;
            var absolute = Path.Combine(solutionDir, "src", "nested", "File.cs");

            var relative = _manager.ToRelativePath(absolute);
            var backToAbsolute = _manager.ToAbsolutePath(relative);

            Assert.AreEqual(absolute, backToAbsolute);
        }
    }
}
