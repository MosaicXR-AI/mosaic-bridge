using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Core.Dispatcher;
using Mosaic.Bridge.Tools.Shared;

namespace Mosaic.Bridge.Tools.Search
{
    public static class SearchMissingReferencesTool
    {
        [MosaicTool("search/missing_references",
                    "Scans all scene GameObjects for components with missing (null) object references",
                    isReadOnly: true)]
        public static ToolResult<SearchMissingReferencesResult> Execute(SearchMissingReferencesParams p)
        {
            var all       = Resources.FindObjectsOfTypeAll<GameObject>();
            var allIssues = new List<MissingRefResult>();

            foreach (var go in all)
            {
                // Story 2.11: Allow cancellation during potentially large scene scans
                ToolExecutionContext.CancellationToken.ThrowIfCancellationRequested();

                if (!go.scene.IsValid()) continue;

                var hierarchyPath = GetHierarchyPath(go.transform);

                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;

                    var so   = new SerializedObject(component);
                    var prop = so.GetIterator();

                    while (prop.NextVisible(true))
                    {
                        if (prop.propertyType == SerializedPropertyType.ObjectReference
                            && prop.objectReferenceValue == null
                            && prop.objectReferenceInstanceIDValue != 0)
                        {
                            allIssues.Add(new MissingRefResult
                            {
                                GameObjectName = go.name,
                                HierarchyPath  = hierarchyPath,
                                ComponentType  = component.GetType().Name,
                                PropertyName   = prop.propertyPath
                            });
                        }
                    }
                }
            }

            int totalCount = allIssues.Count;
            int offset = PaginationHelper.DecodeOffset(p?.PageToken);
            var (page, nextToken) = PaginationHelper.Paginate(allIssues, offset, p?.PageSize ?? 0);

            return ToolResult<SearchMissingReferencesResult>.Ok(new SearchMissingReferencesResult
            {
                Issues        = page,
                Count         = page.Count,
                TotalCount    = totalCount,
                NextPageToken = nextToken
            });
        }

        private static string GetHierarchyPath(Transform t)
        {
            var path    = t.name;
            var current = t.parent;
            while (current != null)
            {
                path    = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }
    }
}
