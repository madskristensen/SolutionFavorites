using System.Collections.Generic;
using System.ComponentModel.Composition;
using EnvDTE;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Utilities;

namespace SolutionFavorites.MEF
{
    /// <summary>
    /// Provides the Favorites node as a child of the solution node in Solution Explorer.
    /// </summary>
    [Export(typeof(IAttachedCollectionSourceProvider))]
    [Name(nameof(FavoritesSourceProvider))]
    [Order(Before = HierarchyItemsProviderNames.Contains)]
    internal class FavoritesSourceProvider : IAttachedCollectionSourceProvider
    {
        private FavoritesRootNode _rootNode;
        private FavoritesSolutionCollectionSource _solutionCollectionSource;
        private readonly DTE _dte;

        public FavoritesSourceProvider()
        {
            _dte = VS.GetRequiredService<DTE, DTE>();
            VS.Events.SolutionEvents.OnBeforeCloseSolution += OnBeforeCloseSolution;
        }

        private void OnBeforeCloseSolution()
        {
            _solutionCollectionSource?.Dispose();
            _solutionCollectionSource = null;
            _rootNode?.Dispose();
            _rootNode = null;
            FavoritesManager.Instance.Clear();
        }

        public IEnumerable<IAttachedRelationship> GetRelationships(object item)
        {
            // Attach to the solution node - provides the Favorites root node
            if (item is IVsHierarchyItem hierarchyItem && 
                HierarchyUtilities.IsSolutionNode(hierarchyItem.HierarchyIdentity))
            {
                yield return Relationships.Contains;
            }
            // Attach to FavoritesRootNode - provides its children (files)
            else if (item is FavoritesRootNode)
            {
                yield return Relationships.Contains;
            }
        }

        public IAttachedCollectionSource CreateCollectionSource(object item, string relationshipName)
        {
            if (relationshipName == KnownRelationships.Contains)
            {
                // For the solution node, return a wrapper that contains the Favorites root node
                if (item is IVsHierarchyItem hierarchyItem && 
                    HierarchyUtilities.IsSolutionNode(hierarchyItem.HierarchyIdentity))
                {
                    if (!string.IsNullOrEmpty(_dte?.Solution?.FullName))
                    {
                        // Load favorites for this solution
                        FavoritesManager.Instance.LoadForSolution(_dte.Solution.FullName);
                        
                        if (_rootNode == null)
                        {
                            _rootNode = new FavoritesRootNode(hierarchyItem);
                        }

                        return _solutionCollectionSource ??= new FavoritesSolutionCollectionSource(hierarchyItem, _rootNode);
                    }
                }
                // For the root node, return itself (it contains the favorite items)
                else if (item is FavoritesRootNode rootNode)
                {
                    return rootNode;
                }
            }

            return null;
        }
    }
}
