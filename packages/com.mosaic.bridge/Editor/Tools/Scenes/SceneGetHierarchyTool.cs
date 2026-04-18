using System.Collections.Generic;
using UnityEngine;
using UnityEditor.SceneManagement;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Tools.Shared;

namespace Mosaic.Bridge.Tools.Scenes
{
    public static class SceneGetHierarchyTool
    {
        [MosaicTool("scene/get_hierarchy",
                    "Returns the full GameObject hierarchy of the currently active scene as a tree",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<SceneGetHierarchyResult> GetHierarchy(SceneGetHierarchyParams p)
        {
            var maxDepth = p?.MaxDepth ?? 5;
            var includeInactive = p?.IncludeInactive ?? true;
            var scene = EditorSceneManager.GetActiveScene();

            // Collect all root nodes (respecting inactive filter)
            var allRoots = new List<HierarchyNode>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!includeInactive && !root.activeInHierarchy)
                    continue;
                allRoots.Add(BuildNode(root, maxDepth, 0, includeInactive));
            }

            int totalCount = allRoots.Count;
            int offset = PaginationHelper.DecodeOffset(p?.PageToken);
            var (page, nextToken) = PaginationHelper.Paginate(allRoots, offset, p?.PageSize ?? 0);

            return ToolResult<SceneGetHierarchyResult>.Ok(new SceneGetHierarchyResult
            {
                Roots         = page,
                TotalCount    = totalCount,
                NextPageToken = nextToken
            });
        }

        private static HierarchyNode BuildNode(GameObject go, int maxDepth, int currentDepth, bool includeInactive)
        {
            var node = new HierarchyNode
            {
                Name = go.name,
                InstanceId = go.GetInstanceID(),
                ActiveSelf = go.activeSelf,
                Children = new List<HierarchyNode>()
            };

            if (currentDepth < maxDepth)
            {
                foreach (Transform child in go.transform)
                {
                    if (!includeInactive && !child.gameObject.activeInHierarchy)
                        continue;
                    node.Children.Add(BuildNode(child.gameObject, maxDepth, currentDepth + 1, includeInactive));
                }
            }

            return node;
        }
    }
}
